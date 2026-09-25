using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsDope.Systems;

/// <summary>
/// Server owner of the addict ledger: loads/saves it with the world, runs the daily pass,
/// finds rumours and exposes the <c>/addicts</c> command. The rules live in <see cref="AddictLedger"/>.
/// </summary>
public class AddictReputationSystem
{
    public static AddictReputationSystem? Instance;

    private const string SaveKey = "vs-dope-addict-ledger";
    private const int MaxCatchUpDays = 30;

    // Rumours point at real worldgen structures the addict "scavenged". GenStructures stores
    // Code as "<schematic>/<structure code>"; the structure codes are vanilla (incl. its spelling).
    private static readonly Dictionary<string, string> RumourKinds = new()
    {
        ["surrfaceruins"] = "ruin",
        ["raresurfaceruins"] = "ruin",
        ["specialsurfaceruins"] = "ruin",
        ["undergroundruin"] = "underground",
        ["buriedtreasurechest"] = "cache",
        ["arcticsupplies"] = "supplies",
    };
    private const int RumourMinDistance = 48;
    private const int RumourMaxDistance = 1200;
    private const int RumourDistanceRounding = 50;

    // How hard each product hits an addict's body (decline exposure per unit bought).
    private static readonly Dictionary<string, float> ExposurePerUnit = new()
    {
        ["vs-dope:opium"] = 1f,
        ["vs-dope:morphine"] = 2f,
        ["vs-dope:coca-vitae"] = 2f,
        ["vs-dope:heroin"] = 0.4f,   // per 0.1 L dose (4 per litre)
    };

    private ICoreServerAPI api = null!;
    private readonly Random rng = new();

    public AddictLedger Ledger { get; private set; } = new();
    public Random Rng => rng;
    public double Today => api.World.Calendar.TotalDays;

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        Instance = this;

        api.Event.SaveGameLoaded += Load;
        api.Event.GameWorldSave += Save;
        api.Event.Timer(CheckDay, 20);

        api.ChatCommands.Create("addicts")
            .WithDescription(Lang.Get("vs-dope:command-addicts-desc"))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(OnAddictsCommand);
    }

    private void Load()
    {
        try
        {
            byte[]? bytes = api.WorldManager.SaveGame.GetData(SaveKey);
            Ledger = new AddictLedger(bytes == null ? null : SerializerUtil.Deserialize<AddictLedgerData>(bytes));
        }
        catch (Exception e)
        {
            // A corrupt ledger must not take the world down; addicts simply start over as strangers.
            api.Logger.Error("[vs-dope] could not read the addict ledger, starting a new one: {0}", e);
            Ledger = new AddictLedger();
        }
    }

    private void Save() => api.WorldManager.SaveGame.StoreData(SaveKey, SerializerUtil.Serialize(Ledger.Data));

    private void CheckDay()
    {
        if (api.World?.Calendar == null) return;
        int today = (int)Today;
        var data = Ledger.Data;
        if (data.LastDailyDay < 0 || today < data.LastDailyDay) { data.LastDailyDay = today; return; }
        int days = Math.Min(today - data.LastDailyDay, MaxCatchUpDays);
        for (int i = 0; i < days; i++) Ledger.DailyPass(today - days + 1 + i);
        data.LastDailyDay = today;
    }

    // ---- lookups used by the entity, trade and spawn systems -------------------

    public static string NameOf(AddictRecord r) => Lang.Get("vs-dope:addict-name-" + r.NameIndex);

    public static string TierLangKey(AddictTier tier) => "vs-dope:addict-tier-" + tier.ToString().ToLowerInvariant();

    public static float ExposureOf(string drugCode)
        => ExposurePerUnit.TryGetValue(drugCode, out float e) ? e : 1f;

    // ---- player-facing consequences ---------------------------------------------

    public void RecordAssault(AddictRecord r, IServerPlayer player)
    {
        if (!Ledger.RecordAssault(r, player.PlayerUID)) return;
        player.SendLocalisedMessage(0, "vs-dope:addict-rep-assault", NameOf(r));
    }

    public void RecordDeath(AddictRecord r, string cause, string? playerUid, bool selfDefence)
    {
        var tierBefore = playerUid == null ? AddictTier.Stranger : AddictLedger.TierOf(r, playerUid);
        if (!Ledger.RecordDeath(r, cause, playerUid, selfDefence, Today)) return;
        if (playerUid == null || api.World.PlayerByUid(playerUid) is not IServerPlayer player) return;
        if (player.ConnectionState != EnumClientState.Playing) return;

        if (cause == AddictDeathCause.Killed && !selfDefence)
            player.SendLocalisedMessage(0, "vs-dope:addict-rep-killed", NameOf(r));
        else if ((cause == AddictDeathCause.Overdose || cause == AddictDeathCause.BadBatch) && tierBefore >= AddictTier.Customer)
            player.SendLocalisedMessage(0, tierBefore >= AddictTier.Regular ? "vs-dope:addict-rep-regular-died" : "vs-dope:addict-rep-customer-died", NameOf(r));
    }

    // ---- rumours ------------------------------------------------------------------

    /// <summary>
    /// A trusted regular tells the player about a real ruin near <paramref name="near"/> it hasn't
    /// mentioned yet. Returns the chat message, or null when there is nothing (new) to tell.
    /// Only searches map regions that are already loaded, like the vanilla structure locator.
    /// </summary>
    public string? TryRumour(AddictRecord r, BlockPos near)
    {
        int regionSize = api.WorldManager.RegionSize;
        var candidates = new List<(string Key, string Kind, Cuboidi Loc, double Dist)>();
        for (int rx = (near.X - RumourMaxDistance) / regionSize; rx <= (near.X + RumourMaxDistance) / regionSize; rx++)
        {
            for (int rz = (near.Z - RumourMaxDistance) / regionSize; rz <= (near.Z + RumourMaxDistance) / regionSize; rz++)
            {
                var region = api.World.BlockAccessor.GetMapRegion(rx, rz);
                if (region?.GeneratedStructures == null) continue;
                foreach (var s in region.GeneratedStructures)
                {
                    if (s?.Code == null || s.Location == null) continue;
                    string structureCode = s.Code.Split('/').Last();
                    if (!RumourKinds.TryGetValue(structureCode, out string? kind)) continue;
                    string key = s.Location.X1 + "," + s.Location.Y1 + "," + s.Location.Z1;
                    if (r.ToldRumours.Contains(key)) continue;
                    double dist = s.Location.ShortestDistanceFrom(near.X, near.Y, near.Z);
                    if (dist < RumourMinDistance || dist > RumourMaxDistance) continue;
                    candidates.Add((key, kind, s.Location, dist));
                }
            }
        }
        if (candidates.Count == 0) return null;

        var pick = candidates[rng.Next(candidates.Count)];
        r.ToldRumours.Add(pick.Key);

        double dx = pick.Loc.Center.X - near.X, dz = pick.Loc.Center.Z - near.Z;
        int distance = Math.Max(RumourDistanceRounding, (int)Math.Round(Math.Sqrt(dx * dx + dz * dz) / RumourDistanceRounding) * RumourDistanceRounding);
        return Lang.Get("vs-dope:addict-rumour", NameOf(r),
            Lang.Get("vs-dope:addict-rumour-kind-" + pick.Kind), distance, Lang.Get("vs-dope:addict-dir-" + Compass(dx, dz)));
    }

    // 8-point compass. In Vintage Story north is -Z and east is +X.
    private static string Compass(double dx, double dz)
    {
        string[] points = { "e", "se", "s", "sw", "w", "nw", "n", "ne" };
        double angle = Math.Atan2(dz, dx); // 0 = east, +pi/2 = south (+Z)
        int index = (int)Math.Round(angle / (Math.PI / 4));
        return points[((index % 8) + 8) % 8];
    }

    // ---- /addicts -----------------------------------------------------------------

    private TextCommandResult OnAddictsCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player) return TextCommandResult.Error(Lang.Get("vs-dope:command-spawnaddict-noplayer"));
        string uid = player.PlayerUID;
        double today = Today;

        var sb = new StringBuilder();
        var known = Ledger.Data.Addicts.Values
            .Where(r => r.PeekRelation(uid) is { } rel && (rel.Visits > 0 || rel.TimesMuggedPlayer > 0 || rel.Grudge))
            .OrderByDescending(r => r.Alive).ThenByDescending(r => r.PeekRelation(uid)!.Visits)
            .ToList();

        sb.AppendLine(Lang.Get("vs-dope:addicts-header", known.Count));
        foreach (var r in known)
        {
            var rel = r.PeekRelation(uid)!;
            string status = r.Alive
                ? Lang.Get("vs-dope:addicts-decline-" + AddictLedger.DeclineStage(r))
                : Lang.Get("vs-dope:addicts-dead-" + r.DeathCause);
            string mood = rel.Grudge ? Lang.Get("vs-dope:addicts-grudge")
                : rel.TimesMuggedPlayer > 0 ? Lang.Get("vs-dope:addicts-mugger") : "";
            sb.AppendLine(Lang.Get("vs-dope:addicts-line", NameOf(r), Lang.Get(TierLangKey(AddictLedger.TierOf(r, uid))),
                rel.Visits, rel.GearsPaid, status, mood).TrimEnd());
        }

        if (Ledger.Data.Players.TryGetValue(uid, out var standing))
        {
            if (standing.Heat >= AddictLedger.HeatStopAt) sb.AppendLine(Lang.Get("vs-dope:addicts-heat-stop"));
            else if (standing.Heat > 0) sb.AppendLine(Lang.Get("vs-dope:addicts-heat", Math.Round(standing.Heat, 1)));
            if (today < standing.DemandLowUntilDay)
                sb.AppendLine(Lang.Get("vs-dope:addicts-demand-low", Math.Ceiling(standing.DemandLowUntilDay - today)));
        }
        return TextCommandResult.Success(sb.ToString().TrimEnd());
    }
}
