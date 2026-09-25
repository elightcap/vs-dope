using System;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VsDope.Entities;

namespace VsDope.Systems;

// Spawns 0-3 drug addicts near a player once per in-game day, plus regulars who come looking
// for their dealers, plus a debug slash command. Who turns up comes from the addict ledger
// (AddictReputationSystem): a returning face or a new one, fewer after violence or a death.
public class DrugAddictSpawnSystem
{
    private const int MaxPerDay = 3;
    private const double MinOffset = 20;
    private const double MaxOffset = 40;
    // Debug spawn lands closer so the tester sees it immediately.
    private const double DebugMinOffset = 6;
    private const double DebugMaxOffset = 14;
    private const int MaxDebugSpawn = 20;
    private static readonly AssetLocation EntityCode = new("vs-dope", "drugaddict");

    private ICoreServerAPI api = null!;
    private int lastProcessedDay = -1;
    private readonly Random rng = new();

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        // Poll frequently; only act when the calendar day actually rolls over.
        api.Event.Timer(Check, 20);
        api.ChatCommands.Create("spawnaddict")
            .WithDescription(Lang.Get("vs-dope:command-spawnaddict-desc"))
            .RequiresPrivilege(Privilege.controlserver)
            .RequiresPlayer()
            .WithArgs(api.ChatCommands.Parsers.OptionalIntRange("count", 1, MaxDebugSpawn, 1),
                      api.ChatCommands.Parsers.OptionalBool("returning"))
            .HandleWith(OnSpawnCommand);
    }

    private TextCommandResult OnSpawnCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player || player.Entity == null)
            return TextCommandResult.Error(Lang.Get("vs-dope:command-spawnaddict-noplayer"));

        int count = (int)args[0];
        // "returning" prefers addicts this player already knows, to test regulars without waiting days.
        bool returning = args.Parsers[1].IsMissing ? false : (bool)args[1];
        int spawned = SpawnNear(player, count, DebugMinOffset, DebugMaxOffset, returning, out string? reason);
        return spawned > 0
            ? TextCommandResult.Success(Lang.Get("vs-dope:command-spawnaddict-success", spawned, count))
            : TextCommandResult.Error(Lang.Get("vs-dope:command-spawnaddict-failed", count, reason ?? "unknown cause"));
    }

    private void Check()
    {
        if (api.World?.Calendar == null) return;
        int today = (int)api.World.Calendar.TotalDays;
        if (today <= 0 || today == lastProcessedDay) return;

        // First run: adopt the current day without spawning, so loading a save doesn't dump a crowd.
        bool firstRun = lastProcessedDay < 0;
        lastProcessedDay = today;
        if (firstRun) return;

        var players = api.Server.Players.Where(p => p.ConnectionState == EnumClientState.Playing).ToList();
        if (players.Count == 0) return;

        // Routine visitors near one random player, fewer when word of violence or a death is around.
        var chosen = players[rng.Next(players.Count)];
        int count = rng.Next(0, MaxPerDay + 1); // 0..3 inclusive
        if (AddictReputationSystem.Instance is { } rep)
        {
            double scaled = count * rep.Ledger.SpawnMultiplier(chosen.PlayerUID, rep.Today);
            count = (int)scaled + (rng.NextDouble() < scaled - (int)scaled ? 1 : 0);
        }
        SpawnNear(chosen, count, MinOffset, MaxOffset, false, out _);

        // Regulars come back on their own to whoever they buy from.
        if (AddictReputationSystem.Instance is not { } reputation) return;
        foreach (var player in players)
        {
            foreach (var record in reputation.Ledger.PickRegularVisits(player.PlayerUID, reputation.Today, rng))
                SpawnNear(player, record, MinOffset, MaxOffset, out _);
        }
    }

    // Places up to `count` addicts in a ring around the player. Returns how many actually spawned.
    private int SpawnNear(IServerPlayer player, int count, double minOffset, double maxOffset, bool preferReturning, out string? reason)
    {
        reason = null;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            // Pick per spawn so the same addict is never chosen twice (it is active once spawned).
            var record = AddictReputationSystem.Instance is { } rep
                ? rep.Ledger.PickForSpawn(player.PlayerUID, rep.Today, rep.Rng, preferReturning)
                : null;
            if (SpawnNear(player, record, minOffset, maxOffset, out string? why)) spawned++;
            else reason = why;
        }
        return spawned;
    }

    // Spawns one addict (a known identity, or none when the ledger is unavailable) near the player.
    private bool SpawnNear(IServerPlayer player, AddictRecord? record, double minOffset, double maxOffset, out string? reason)
    {
        reason = null;
        var anchor = player.Entity?.Pos;
        if (anchor == null) { reason = "no player/world"; return false; }

        EntityProperties? props;
        try { props = api.World.GetEntityType(EntityCode); }
        catch (Exception e) { reason = $"GetEntityType threw: {e.Message}"; return false; }
        if (props == null) { reason = $"entitytype '{EntityCode}' not loaded"; return false; }

        return TrySpawnOnce(anchor, player.PlayerUID, record, minOffset, maxOffset, props, out reason);
    }

    private bool TrySpawnOnce(EntityPos anchor, string targetUid, AddictRecord? record, double minOffset, double maxOffset, EntityProperties props, out string? reason)
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

            Entity? entity;
            try { entity = api.ClassRegistry.CreateEntity(props); }
            catch (Exception e) { reason = $"CreateEntity threw: {e.Message}"; return false; }

            if (entity == null) { reason = "CreateEntity returned null"; return false; }
            if (entity is not EntityDrugAddict addict) { reason = $"class mismatch: created {entity.GetType().Name}, not EntityDrugAddict (check JSON \"class\" vs RegisterEntity)"; return false; }

            try
            {
                addict.Pos.SetPos(pos);
                addict.BindTarget(targetUid);
                if (record != null) addict.BindIdentity(record);
                api.World.SpawnEntity(addict);
                // EntityId is assigned (and possibly reassigned) inside SpawnEntity.
                if (record != null && AddictReputationSystem.Instance is { } rep) rep.Ledger.MarkActive(record, addict.EntityId, rep.Today);
                return true;
            }
            catch (Exception e) { reason = $"SpawnEntity threw: {e.Message}"; return false; }
        }

        reason ??= "no valid spawn position found";
        return false;
    }
}

