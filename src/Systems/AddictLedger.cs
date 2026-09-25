using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ProtoBuf;

namespace VsDope.Systems;

// How well an addict knows a player. Earned by visits that ended in a sale.
public enum AddictTier { Stranger, Customer, Regular, Trusted }

// Why an addict died. Stored as a string in the save so new causes never shift old values.
public static class AddictDeathCause
{
    public const string Overdose = "overdose";   // ODed on product it just bought
    public const string BadBatch = "badbatch";   // reserved for product purity (#49)
    public const string Killed = "killed";       // killed by a player
    public const string Other = "other";         // fall, drowning, creatures...
}

// The persisted world ledger. Every [ProtoMember] number is part of the save format:
// never renumber or reuse one, only append. (ImplicitFields numbers alphabetically,
// so a new field would shift the tags of old saves.)
// protobuf-net omits a value equal to the member's default (0/false unless [DefaultValue] says
// otherwise) and then keeps the field initializer on load. Any field initialized to something
// else MUST carry a matching [DefaultValue], or e.g. a dead addict (Alive = false) loads alive.
[ProtoContract]
public class AddictLedgerData
{
    [ProtoMember(1), DefaultValue(1)] public int NextId = 1;
    [ProtoMember(2)] public Dictionary<int, AddictRecord> Addicts = new();
    [ProtoMember(3)] public Dictionary<string, PlayerAddictStanding> Players = new();
    [ProtoMember(4), DefaultValue(-1)] public int LastDailyDay = -1;
}

// One addict identity. Survives despawns: a returning regular is a new entity bound to the same record.
[ProtoContract]
public class AddictRecord
{
    [ProtoMember(1)] public int Id;
    [ProtoMember(2)] public int NameIndex;
    [ProtoMember(3), DefaultValue(true)] public bool Alive = true;
    [ProtoMember(4)] public string DeathCause = "";
    [ProtoMember(5)] public string DeathPlayerUid = "";   // killer, or the player whose product it died on
    [ProtoMember(6)] public double DiedDay;
    [ProtoMember(7)] public double CreatedDay;
    [ProtoMember(8)] public double LastSeenDay;
    [ProtoMember(9), DefaultValue(-1.0)] public double LastFedDay = -1;      // last day it bought drugs from anyone
    [ProtoMember(10)] public float Exposure;              // weighted drug units; drives visible decline
    [ProtoMember(11)] public bool Stubborn;               // never recovers when cut off
    [ProtoMember(12)] public long ActiveEntityId;         // entity currently playing this addict, 0 = none
    [ProtoMember(13)] public double ActiveSinceDay;
    [ProtoMember(14)] public Dictionary<string, AddictRelation> Relations = new();
    [ProtoMember(15)] public List<string> ToldRumours = new();   // structure keys already shared

    public AddictRelation RelationWith(string playerUid)
    {
        if (!Relations.TryGetValue(playerUid, out var rel)) Relations[playerUid] = rel = new AddictRelation();
        return rel;
    }

    public AddictRelation? PeekRelation(string playerUid)
        => Relations.TryGetValue(playerUid, out var rel) ? rel : null;
}

// What one addict remembers about one player.
[ProtoContract]
public class AddictRelation
{
    [ProtoMember(1)] public int Visits;              // visits that ended in at least one sale
    [ProtoMember(2)] public int UnitsBought;
    [ProtoMember(3)] public int GearsPaid;
    [ProtoMember(4)] public int TimesMuggedPlayer;   // this addict robbed the player
    [ProtoMember(5)] public bool Grudge;             // the player hurt it unprovoked; it won't come back
    [ProtoMember(6), DefaultValue(-1.0)] public double LastSaleDay = -1;
}

// World-wide consequences for one player.
[ProtoContract]
public class PlayerAddictStanding
{
    [ProtoMember(1)] public float Heat;                  // violence against addicts; decays daily
    [ProtoMember(2)] public double DemandLowUntilDay;    // a customer died on the player's product
}

/// <summary>
/// The addict memory/reputation rules on top of <see cref="AddictLedgerData"/>. Pure logic with no
/// game API calls: callers pass the current calendar day, so the probe can test it directly.
/// </summary>
public class AddictLedger
{
    // ---- tiers (visits with a sale) ------------------------------------------
    public const int CustomerVisits = 1;
    public const int RegularVisits = 3;
    public const int TrustedVisits = 6;

    // Price multiplier and mugging chance per tier (Stranger, Customer, Regular, Trusted).
    private static readonly float[] PriceMultipliers = { 1f, 1f, 1.2f, 1.35f };
    private static readonly double[] MugChances = { 0.12, 0.08, 0.03, 0 };
    // Extra rusty gears a returning addict carries, so it can afford the better prices.
    private static readonly int[] BonusGears = { 0, 2, 6, 10 };
    // Daily chance that an idle regular looks up this player on its own.
    private static readonly double[] VisitChances = { 0, 0, 0.25, 0.4 };

    // ---- returning / pool ----------------------------------------------------
    public const double ReturnChance = 0.4;          // a routine spawn is someone this player already knows
    public const int MaxRegularVisitsPerDay = 2;
    public const int MaxLivingAddicts = 40;           // beyond this, spawns reuse old faces
    public const int MaxDeadRecords = 40;             // oldest dead records are pruned
    public const double StaleActiveDays = 1;          // an "active" addict unseen this long is released

    // ---- heat (violence spreads) ---------------------------------------------
    public const float HeatPerKill = 3f;
    public const float HeatPerAssault = 1f;
    public const float HeatDecayPerDay = 0.5f;
    public const float HeatStopAt = 4f;               // at or above: nobody comes at all

    // ---- deaths lower demand -------------------------------------------------
    public const double DemandLowDaysRegular = 5;
    public const double DemandLowDaysCustomer = 2;
    public const double DemandLowMultiplier = 0.5;

    // ---- decline -------------------------------------------------------------
    public static readonly float[] DeclineThresholds = { 8f, 24f, 48f };   // exposure for stage 1, 2, 3
    public const double CutOffDays = 3;               // days without buying before recovery starts
    public const float RecoveryPerDay = 0.8f;         // exposure multiplier per cut-off day
    public const double StubbornChance = 0.4;
    public const double OverdoseChancePerStage = 0.02; // added to the per-sale OD chance

    // ---- rumours -------------------------------------------------------------
    public const double RumourChance = 0.35;          // per visit, trusted only

    public const int NameCount = 24;                  // lang keys addict-name-0 .. addict-name-23

    public AddictLedgerData Data { get; private set; }

    public AddictLedger(AddictLedgerData? data = null) => Data = data ?? new AddictLedgerData();

    public AddictRecord? Get(int id) => Data.Addicts.TryGetValue(id, out var r) ? r : null;

    public PlayerAddictStanding StandingOf(string playerUid)
    {
        if (!Data.Players.TryGetValue(playerUid, out var s)) Data.Players[playerUid] = s = new PlayerAddictStanding();
        return s;
    }

    // ---- identities ------------------------------------------------------------

    public AddictRecord Create(double today, Random rng)
    {
        var used = Data.Addicts.Values.Where(r => r.Alive).Select(r => r.NameIndex).ToHashSet();
        var free = Enumerable.Range(0, NameCount).Where(i => !used.Contains(i)).ToList();
        var record = new AddictRecord
        {
            Id = Data.NextId++,
            NameIndex = free.Count > 0 ? free[rng.Next(free.Count)] : rng.Next(NameCount),
            CreatedDay = today,
            LastSeenDay = today,
            Stubborn = rng.NextDouble() < StubbornChance,
        };
        Data.Addicts[record.Id] = record;
        return record;
    }

    /// <summary>
    /// Who shows up for a routine spawn near this player: sometimes someone they already know,
    /// otherwise a new face. Once the living pool is full, old faces are reused instead.
    /// </summary>
    public AddictRecord PickForSpawn(string playerUid, double today, Random rng, bool preferReturning = false)
    {
        var idle = Data.Addicts.Values.Where(r => IsIdle(r) && !Refuses(r, playerUid)).ToList();
        var known = idle.Where(r => r.PeekRelation(playerUid) is { Visits: > 0 }).ToList();
        int living = Data.Addicts.Values.Count(r => r.Alive);

        if (known.Count > 0 && (preferReturning || rng.NextDouble() < ReturnChance))
            return WeightedPick(known, r => 1 + r.PeekRelation(playerUid)!.Visits, rng);
        if (living >= MaxLivingAddicts && idle.Count > 0)
            return idle[rng.Next(idle.Count)];
        return Create(today, rng);
    }

    /// <summary>Regulars who come looking for this player today, on top of the routine spawns.</summary>
    public List<AddictRecord> PickRegularVisits(string playerUid, double today, Random rng)
    {
        double mult = SpawnMultiplier(playerUid, today);
        var visitors = new List<AddictRecord>();
        foreach (var r in Data.Addicts.Values.Where(r => IsIdle(r) && !Refuses(r, playerUid)).OrderBy(_ => rng.Next()))
        {
            if (visitors.Count >= MaxRegularVisitsPerDay) break;
            if (rng.NextDouble() < VisitChances[(int)TierOf(r, playerUid)] * mult) visitors.Add(r);
        }
        return visitors;
    }

    public static bool IsIdle(AddictRecord r) => r.Alive && r.ActiveEntityId == 0;

    // It robbed this player, or this player hurt it: it keeps away.
    public static bool Refuses(AddictRecord r, string playerUid)
        => r.PeekRelation(playerUid) is { } rel && (rel.Grudge || rel.TimesMuggedPlayer > 0);

    public void MarkActive(AddictRecord r, long entityId, double today)
    {
        r.ActiveEntityId = entityId;
        r.ActiveSinceDay = today;
        r.LastSeenDay = today;
    }

    public void Release(AddictRecord r, long entityId, double today)
    {
        if (r.ActiveEntityId != entityId) return;
        r.ActiveEntityId = 0;
        r.LastSeenDay = today;
    }

    /// <summary>True when another entity (or none) is playing this addict: this one is a stale copy.</summary>
    public static bool IsStaleEntity(AddictRecord r, long entityId) => !r.Alive || r.ActiveEntityId != entityId;

    // ---- tiers and prices ------------------------------------------------------

    public static AddictTier TierOf(AddictRecord r, string? playerUid)
    {
        int visits = playerUid == null ? 0 : r.PeekRelation(playerUid)?.Visits ?? 0;
        if (visits >= TrustedVisits) return AddictTier.Trusted;
        if (visits >= RegularVisits) return AddictTier.Regular;
        if (visits >= CustomerVisits) return AddictTier.Customer;
        return AddictTier.Stranger;
    }

    public static int PriceFor(int basePrice, AddictTier tier)
    {
        float mult = PriceMultipliers[(int)tier];
        return mult <= 1f ? basePrice : Math.Max(basePrice + 1, (int)Math.Round(basePrice * mult));
    }

    public static double MugChance(AddictTier tier) => MugChances[(int)tier];
    public static int BonusGearsFor(AddictTier tier) => BonusGears[(int)tier];

    /// <summary>
    /// Scales how many addicts come for this player: heat from violence (none at all at the
    /// threshold) and a recent death on their product.
    /// </summary>
    public double SpawnMultiplier(string playerUid, double today)
    {
        if (!Data.Players.TryGetValue(playerUid, out var s)) return 1;
        if (s.Heat >= HeatStopAt) return 0;
        double mult = 1 - s.Heat / HeatStopAt;
        if (today < s.DemandLowUntilDay) mult *= DemandLowMultiplier;
        return mult;
    }

    // ---- events -----------------------------------------------------------------

    /// <summary>A sale. <paramref name="firstOfVisit"/> counts the visit towards the tier once.</summary>
    public void RecordSale(AddictRecord r, string playerUid, int units, int gears, float exposurePerUnit, bool firstOfVisit, double today)
    {
        var rel = r.RelationWith(playerUid);
        if (firstOfVisit) rel.Visits++;
        rel.UnitsBought += units;
        rel.GearsPaid += gears;
        rel.LastSaleDay = today;
        r.LastFedDay = today;
        r.LastSeenDay = today;
        r.Exposure += units * exposurePerUnit;
    }

    public void RecordMugging(AddictRecord r, string playerUid) => r.RelationWith(playerUid).TimesMuggedPlayer++;

    /// <summary>The player hurt this addict unprovoked. Returns true the first time (heat applied).</summary>
    public bool RecordAssault(AddictRecord r, string playerUid)
    {
        var rel = r.RelationWith(playerUid);
        if (rel.Grudge) return false;
        rel.Grudge = true;
        StandingOf(playerUid).Heat += HeatPerAssault;
        return true;
    }

    /// <summary>
    /// Records a death exactly once (false if it was already dead). A killing adds heat unless it
    /// was self-defence; an overdose on a player's product lowers that player's demand.
    /// </summary>
    public bool RecordDeath(AddictRecord r, string cause, string? playerUid, bool selfDefence, double today)
    {
        if (!r.Alive) return false;
        r.Alive = false;
        r.DeathCause = cause;
        r.DeathPlayerUid = playerUid ?? "";
        r.DiedDay = today;
        r.ActiveEntityId = 0;

        if (!string.IsNullOrEmpty(playerUid))
        {
            var standing = StandingOf(playerUid);
            if (cause == AddictDeathCause.Killed && !selfDefence)
            {
                // A grudge assault already cost HeatPerAssault; a kill tops it up to HeatPerKill.
                bool assaulted = r.PeekRelation(playerUid)?.Grudge == true;
                standing.Heat += HeatPerKill - (assaulted ? HeatPerAssault : 0);
            }
            else if (cause == AddictDeathCause.Overdose || cause == AddictDeathCause.BadBatch)
            {
                double days = TierOf(r, playerUid) >= AddictTier.Regular ? DemandLowDaysRegular
                    : TierOf(r, playerUid) >= AddictTier.Customer ? DemandLowDaysCustomer : 0;
                if (days > 0) standing.DemandLowUntilDay = Math.Max(standing.DemandLowUntilDay, today + days);
            }
        }
        PruneDead();
        return true;
    }

    // ---- daily pass -------------------------------------------------------------

    /// <summary>Heat cools off, cut-off addicts recover, stale active flags are released.</summary>
    public void DailyPass(double today)
    {
        foreach (var s in Data.Players.Values) s.Heat = Math.Max(0, s.Heat - HeatDecayPerDay);

        foreach (var r in Data.Addicts.Values.Where(r => r.Alive))
        {
            if (r.ActiveEntityId != 0 && today - r.ActiveSinceDay >= StaleActiveDays) r.ActiveEntityId = 0;
            bool cutOff = r.LastFedDay < 0 || today - r.LastFedDay >= CutOffDays;
            if (cutOff && !r.Stubborn) r.Exposure *= RecoveryPerDay;
        }
    }

    public static int DeclineStage(AddictRecord r)
    {
        int stage = 0;
        foreach (float t in DeclineThresholds) if (r.Exposure >= t) stage++;
        return stage;
    }

    public static double OverdoseChanceFor(double baseChance, AddictRecord r)
        => baseChance + OverdoseChancePerStage * DeclineStage(r);

    public void PruneDead()
    {
        var dead = Data.Addicts.Values.Where(r => !r.Alive).OrderBy(r => r.DiedDay).ToList();
        for (int i = 0; i < dead.Count - MaxDeadRecords; i++) Data.Addicts.Remove(dead[i].Id);
    }

    private static AddictRecord WeightedPick(List<AddictRecord> list, Func<AddictRecord, int> weight, Random rng)
    {
        int total = list.Sum(weight);
        int roll = rng.Next(total);
        foreach (var r in list)
        {
            roll -= weight(r);
            if (roll < 0) return r;
        }
        return list[^1];
    }
}
