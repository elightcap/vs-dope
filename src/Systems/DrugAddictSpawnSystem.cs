using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VsDope.Entities;

namespace VsDope.Systems;

// Spawns 0-3 drug addicts near players once per in-game day, plus a debug slash command.
public class DrugAddictSpawnSystem
{
    private const int MaxPerDay = 3;
    private const double MinOffset = 20;
    private const double MaxOffset = 40;
    // Debug spawn lands closer so the tester sees it immediately.
    private const double DebugMinOffset = 6;
    private const double DebugMaxOffset = 14;
    private static readonly AssetLocation EntityCode = new("vs-dope", "drugaddict");

    private ICoreServerAPI api;
    private int lastProcessedDay = -1;
    private readonly Random rng = new();

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        // Poll frequently; only act when the calendar day actually rolls over.
        api.Event.Timer(Check, 20);
        api.RegisterCommand("spawnaddict", "Spawn a drug addict NPC near you (for testing)", "/spawnaddict [count]", OnSpawnCommand, Privilege.controlserver);

        // One-shot diagnostic: confirm the entity asset registered after world load.
        DumpRegistry("startup");
    }

    private void DumpRegistry(string tag)
    {
        try
        {
            var codes = api?.World?.EntityTypeCodes;
            if (codes == null) { api.Logger.Error($"[vs-dope-diag:{tag}] EntityTypeCodes is null"); return; }
            var matches = codes.Where(c => c.Contains("dope", StringComparison.OrdinalIgnoreCase) || c.Contains("addict", StringComparison.OrdinalIgnoreCase)).ToList();
            api.Logger.Event($"[vs-dope-diag:{tag}] entity type count={codes.Count}; vs-dope/addict matches=[{string.Join(", ", matches)}]; sample(first 25)=[{string.Join(", ", codes.Take(25))}]");
        }
        catch (Exception e) { api.Logger.Error($"[vs-dope-diag:{tag}] threw: {e}"); }
    }

    private void OnSpawnCommand(IServerPlayer player, int groupId, CmdArgs args)
    {
        if (player?.Entity == null) return;

        int count = 1;
        if (args.Length > 0)
        {
            try { count = args.PopInt() ?? 1; } catch { count = 1; }
        }
        count = Math.Max(1, Math.Min(count, 20));

        int spawned = SpawnNear(player, count, DebugMinOffset, DebugMaxOffset, out string reason);
        string detail = spawned > 0 ? "" : (string.IsNullOrEmpty(reason) ? " unknown cause" : $" ({reason})");
        player.SendMessage(
            groupId,
            $"[vs-dope] Spawned {spawned}/{count} drug addict(s) near you.{detail}",
            spawned > 0 ? EnumChatType.CommandSuccess : EnumChatType.CommandError,
            null);
    }

    private void Check()
    {
        if (api?.World?.Calendar == null) return;
        int today = (int)api.World.Calendar.TotalDays;
        if (today <= 0 || today == lastProcessedDay) return;

        // First run: adopt the current day without spawning, so loading a save doesn't dump a crowd.
        bool firstRun = lastProcessedDay < 0;
        lastProcessedDay = today;
        if (firstRun) return;

        int count = rng.Next(0, MaxPerDay + 1); // 0..3 inclusive
        var players = api.Server.Players.Where(p => p.ConnectionState == EnumClientState.Playing).ToList();
        if (players.Count == 0) return;

        SpawnNear(players[rng.Next(players.Count)], count, MinOffset, MaxOffset);
    }

    // Places up to `count` addicts in a ring around the player. Returns how many actually spawned.
    public int SpawnNear(IServerPlayer player, int count, double minOffset, double maxOffset)
        => SpawnNear(player, count, minOffset, maxOffset, out _);

    public int SpawnNear(IServerPlayer player, int count, double minOffset, double maxOffset, out string reason)
    {
        reason = null;
        var anchor = player?.Entity?.Pos;
        if (anchor == null || api?.World == null) { reason = "no player/world"; return 0; }

        EntityProperties props;
        try { props = api.World.GetEntityType(EntityCode); }
        catch (Exception e) { reason = $"GetEntityType threw: {e.Message}"; return 0; }
        if (props == null) { reason = $"entitytype '{EntityCode}' not loaded"; DumpRegistry("command"); return 0; }

        int spawned = 0;
        string lastReason = null;
        for (int i = 0; i < count; i++)
        {
            if (TrySpawnOnce(anchor, player.PlayerUID, minOffset, maxOffset, props, out string why))
                spawned++;
            else lastReason = why;
        }

        reason = lastReason;
        return spawned;
    }

    private bool TrySpawnOnce(EntityPos anchor, string targetUid, double minOffset, double maxOffset, EntityProperties props, out string reason)
    {
        reason = null;
        // Candidate spots: a ring around the player, then a guaranteed point right beside them.
        for (int attempt = 0; attempt <= 8; attempt++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double dist = attempt < 8 ? minOffset + rng.NextDouble() * (maxOffset - minOffset) : 1.5; // last try: right next to player
            int x = (int)Math.Round(anchor.X + Math.Cos(angle) * dist);
            int z = (int)Math.Round(anchor.Z + Math.Sin(angle) * dist);

            double y;
            try
            {
                int groundY = api.World.BlockAccessor.GetRainMapHeightAt(x, z);
                // Rainmap height is the surface; if it looks bogus (e.g. player in a cave), use the player's own level.
                y = groundY > 1 ? groundY + 1 : anchor.Y;
            }
            catch (Exception e) { reason = $"height query threw: {e.Message}"; continue; }

            var pos = new Vec3d(x + 0.5, y, z + 0.5);

            Entity entity;
            try { entity = api.ClassRegistry.CreateEntity(props); }
            catch (Exception e) { reason = $"CreateEntity threw: {e.Message}"; return false; }

            if (entity == null) { reason = "CreateEntity returned null"; return false; }
            var addict = entity as EntityDrugAddict;
            if (addict == null) { reason = $"class mismatch: created {entity.GetType().Name}, not EntityDrugAddict (check JSON \"class\" vs RegisterEntity)"; return false; }

            try
            {
                addict.ServerPos.SetPos(pos);
                addict.BindTarget(targetUid);
                api.World.SpawnEntity(addict);
                return true;
            }
            catch (Exception e) { reason = $"SpawnEntity threw: {e.Message}"; return false; }
        }

        reason ??= "no valid spawn position found";
        return false;
    }
}

