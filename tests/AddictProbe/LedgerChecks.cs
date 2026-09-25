using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using VsDope.Entities;
using VsDope.Systems;

// Addict memory/reputation (#51): pure ledger rules, save roundtrip, and the live entity binding.
public static class LedgerChecks
{
    public static void Rules(Action<bool, string> check)
    {
        var rng = new Random(51);
        var ledger = new AddictLedger();
        const string me = "player-a", other = "player-b";

        // Identities and tiers.
        var a = ledger.Create(10, rng);
        var b = ledger.Create(10, rng);
        check(a.Id != b.Id && a.NameIndex != b.NameIndex, "new addicts get distinct ids and names");
        check(AddictLedger.TierOf(a, me) == AddictTier.Stranger, "unknown addict is a stranger");

        // Visits, not sale clicks, build the tier.
        ledger.RecordSale(a, me, 1, 5, 1f, firstOfVisit: true, 10);
        for (int i = 0; i < 5; i++) ledger.RecordSale(a, me, 1, 5, 1f, firstOfVisit: false, 10);
        check(a.RelationWith(me).Visits == 1 && AddictLedger.TierOf(a, me) == AddictTier.Customer, "six sales in one visit = one visit (customer)");
        ledger.RecordSale(a, me, 1, 5, 1f, true, 11);
        ledger.RecordSale(a, me, 1, 5, 1f, true, 12);
        check(AddictLedger.TierOf(a, me) == AddictTier.Regular, "three visits = regular");
        check(AddictLedger.TierOf(a, other) == AddictTier.Stranger, "relations are per player");
        check(AddictLedger.PriceFor(30, AddictTier.Regular) == 36 && AddictLedger.PriceFor(5, AddictTier.Trusted) > 5
              && AddictLedger.PriceFor(12, AddictTier.Customer) == 12, "regulars pay more, customers the base price");
        check(AddictLedger.MugChance(AddictTier.Trusted) == 0 && AddictLedger.MugChance(AddictTier.Stranger) > AddictLedger.MugChance(AddictTier.Regular),
              "known customers mug less");

        // Returning: an active addict is never picked; an idle known one comes back.
        ledger.MarkActive(a, 500, 12);
        check(AddictLedger.IsStaleEntity(a, 499) && !AddictLedger.IsStaleEntity(a, 500), "only the active entity plays the addict");
        for (int i = 0; i < 20; i++) check(ledger.PickForSpawn(me, 12, rng).Id != a.Id, "active addict not re-picked " + i);
        ledger.Release(a, 500, 12);
        check(ledger.PickForSpawn(me, 12, rng, preferReturning: true).Id == a.Id, "idle regular returns (preferReturning)");
        ledger.Release(a, 999, 12);   // a stale copy's release must not free someone else's claim
        ledger.MarkActive(a, 600, 12);
        ledger.Release(a, 999, 12);
        check(a.ActiveEntityId == 600, "releasing from a stale entity is ignored");
        ledger.DailyPass(13.5);
        check(a.ActiveEntityId == 0, "stale active flag released after a day");

        int visits = 0;
        for (int i = 0; i < 200; i++) visits += ledger.PickRegularVisits(me, 14, rng).Count(r => r.Id == a.Id);
        check(visits > 20 && visits < 90, "regular looks the player up on its own ~25% of days (" + visits + "/200)");

        // Muggers and grudges keep away.
        var mugger = ledger.Create(14, rng);
        ledger.RecordSale(mugger, me, 1, 1, 1f, true, 14);
        ledger.RecordMugging(mugger, me);
        for (int i = 0; i < 30; i++) check(ledger.PickForSpawn(me, 14, rng, true).Id != mugger.Id, "mugger never returns to its victim " + i);

        // Heat: an assault then a kill = one kill's worth; self-defence is free; it decays.
        var victim = ledger.Create(14, rng);
        check(ledger.RecordAssault(victim, other) && !ledger.RecordAssault(victim, other), "assault heat applies once");
        check(ledger.RecordDeath(victim, AddictDeathCause.Killed, other, false, 14), "death recorded");
        check(!ledger.RecordDeath(victim, AddictDeathCause.Killed, other, false, 14), "death recorded only once");
        check(Math.Abs(ledger.StandingOf(other).Heat - AddictLedger.HeatPerKill) < 1e-4, "assault + kill = HeatPerKill");
        check(ledger.SpawnMultiplier(other, 14) < 1, "heat lowers spawns");
        ledger.StandingOf(other).Heat = AddictLedger.HeatStopAt;
        check(ledger.SpawnMultiplier(other, 14) == 0, "heat at threshold stops all visits");
        var selfDefence = ledger.Create(14, rng);
        ledger.RecordDeath(selfDefence, AddictDeathCause.Killed, me, selfDefence: true, 14);
        check(ledger.StandingOf(me).Heat == 0, "killing a mugger in self-defence adds no heat");
        ledger.StandingOf(other).Heat = 1;
        ledger.DailyPass(15);
        check(Math.Abs(ledger.StandingOf(other).Heat - (1 - AddictLedger.HeatDecayPerDay)) < 1e-4, "heat decays daily");

        // A regular's overdose on your product lowers demand for a while.
        ledger.RecordDeath(a, AddictDeathCause.Overdose, me, false, 15);
        check(!a.Alive && a.DeathCause == AddictDeathCause.Overdose && a.ActiveEntityId == 0, "overdose death stored");
        check(ledger.SpawnMultiplier(me, 16) == AddictLedger.DemandLowMultiplier, "demand low after a regular's overdose");
        check(ledger.SpawnMultiplier(me, 15 + AddictLedger.DemandLowDaysRegular) == 1, "demand recovers");
        check(ledger.PickRegularVisits(me, 16, rng).All(r => r.Alive), "the dead never visit");

        // Decline and recovery.
        var user = ledger.Create(20, rng);
        user.Stubborn = false;
        var stubborn = ledger.Create(20, rng);
        stubborn.Stubborn = true;
        foreach (var r in new[] { user, stubborn }) ledger.RecordSale(r, me, 13, 50, 4f, true, 20);   // 52 exposure
        check(AddictLedger.DeclineStage(user) == 3, "heavy use reaches decline stage 3");
        check(AddictLedger.OverdoseChanceFor(0.12, user) > 0.12, "a declined body overdoses more easily");
        for (int d = 21; d <= 30; d++) ledger.DailyPass(d);
        check(AddictLedger.DeclineStage(user) < 3 && user.Exposure < 52, "cut off addict recovers");
        check(AddictLedger.DeclineStage(stubborn) == 3, "stubborn addict does not recover");

        // Save format roundtrip (explicit ProtoMember numbers).
        var bytes = SerializerUtil.Serialize(ledger.Data);
        var back = new AddictLedger(SerializerUtil.Deserialize<AddictLedgerData>(bytes));
        var bu = back.Get(user.Id)!;
        check(back.Data.NextId == ledger.Data.NextId && back.Data.Addicts.Count == ledger.Data.Addicts.Count, "ledger roundtrip keeps every record");
        check(Math.Abs(bu.Exposure - user.Exposure) < 1e-4 && bu.RelationWith(me).Visits == 1 && bu.NameIndex == user.NameIndex, "record roundtrip keeps relations and exposure");
        check(!back.Get(a.Id)!.Alive && back.StandingOf(me).DemandLowUntilDay == ledger.StandingOf(me).DemandLowUntilDay, "roundtrip keeps deaths and standing");
        check(back.Get(mugger.Id)!.RelationWith(me).TimesMuggedPlayer == 1, "roundtrip keeps muggings");
        // protobuf-net omits values equal to the default: non-zero initializers need [DefaultValue].
        var fresh = new AddictLedger();
        var never = fresh.Create(0, rng);
        var fedAtZero = fresh.Create(0, rng);
        fresh.RecordSale(fedAtZero, me, 1, 1, 1f, true, 0);
        var fb = new AddictLedger(SerializerUtil.Deserialize<AddictLedgerData>(SerializerUtil.Serialize(fresh.Data)));
        check(fb.Get(never.Id)!.LastFedDay == -1 && fb.Get(fedAtZero.Id)!.LastFedDay == 0, "roundtrip keeps LastFedDay -1 and 0");
        check(fb.Get(fedAtZero.Id)!.RelationWith(me).LastSaleDay == 0 && fb.Data.LastDailyDay == -1, "roundtrip keeps other sentinels");

        // Pool limits.
        var big = new AddictLedger();
        for (int i = 0; i < AddictLedger.MaxDeadRecords + 5; i++) big.Create(1, rng);   // also above MaxLivingAddicts
        int before = big.Data.Addicts.Count;
        big.PickForSpawn(me, 1, rng);
        check(big.Data.Addicts.Count == before, "full pool reuses old faces");
        foreach (var r in big.Data.Addicts.Values.ToList()) big.RecordDeath(r, AddictDeathCause.Other, null, false, r.Id);
        check(big.Data.Addicts.Count == AddictLedger.MaxDeadRecords, "old dead records are pruned");
    }

    // Needs the live server: the reputation system, the entity type and a loaded chunk.
    public static void Entity(ICoreServerAPI api, EntityProperties props, Vec3d at, Action<bool, string> check)
    {
        var rep = AddictReputationSystem.Instance;
        check(rep != null, "reputation system running");
        var world = api.World;
        var record = rep!.Ledger.Create(rep.Today, rep.Rng);
        record.Exposure = AddictLedger.DeclineThresholds[1];   // stage 2

        var addict = (EntityDrugAddict)api.ClassRegistry.CreateEntity(props);
        addict.Pos.SetPos(at);
        addict.BindIdentity(record);
        world.SpawnEntity(addict);
        rep.Ledger.MarkActive(record, addict.EntityId, rep.Today);

        check(addict.Record == record, "spawned addict is bound to its record");
        check(addict.WatchedAttributes.GetInt("textureIndex") == 2, "decline stage survives Initialize as textureIndex");
        check(addict.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name") == AddictReputationSystem.NameOf(record), "name tag set from the ledger");
        check(!AddictReputationSystem.NameOf(record).StartsWith("vs-dope:"), "addict name lang key resolves");
        check(addict.ConsumeFirstSaleOfVisit() && !addict.ConsumeFirstSaleOfVisit(), "first sale of a visit counted once");

        // A leftover copy of an addict that another entity now plays despawns on its first tick.
        var copy = (EntityDrugAddict)api.ClassRegistry.CreateEntity(props);
        copy.Pos.SetPos(at.AddCopy(3, 0, 0));
        copy.BindIdentity(record);
        world.SpawnEntity(copy);
        copy.OnGameTick(0.05f);
        check(!copy.Alive || copy.DespawnReason != null || world.GetEntityById(copy.EntityId) == null, "stale copy despawns");

        // A legacy addict (no identity) adopts one on its first tick and keeps the healthy skin.
        var legacy = (EntityDrugAddict)api.ClassRegistry.CreateEntity(props);
        legacy.Pos.SetPos(at.AddCopy(-3, 0, 0));
        world.SpawnEntity(legacy);
        check(legacy.WatchedAttributes.GetInt("textureIndex", -1) == 0, "legacy addict keeps the base texture");
        legacy.OnGameTick(0.05f);
        check(legacy.Record is { Alive: true } lr && lr.ActiveEntityId == legacy.EntityId, "legacy addict adopted into the ledger");

        // Death is recorded once, with its cause.
        addict.Overdose();
        check(!record.Alive && record.DeathCause == AddictDeathCause.Overdose, "overdose recorded in the ledger");
        addict.Die(EnumDespawnReason.Death, new DamageSource { Source = EnumDamageSource.Internal });
        check(record.DeathCause == AddictDeathCause.Overdose, "second Die does not overwrite the death");

        api.World.DespawnEntity(legacy, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
    }
}
