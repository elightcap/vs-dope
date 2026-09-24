using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VsDope.Systems;

/// <summary>
/// Server-owned overdose state. Every dose rolls a random overdose chance: a per-drug base
/// chance plus a per-drug increase for each other dose (any drug) taken inside a rolling
/// game-hour window, reduced by the product's tolerance. A successful roll starts (or worsens)
/// an overdose whose severity recovers over game time. While overdosing the player is slowed,
/// drug healing is blocked, and above a severity gate the player takes poison damage.
/// </summary>
public sealed class OverdoseSystem
{
    public const string WatchActive = "vs-dope-overdose";
    /// <summary>Chance (0..1) that the next dose of the last-used product overdoses. Drives the HUD warning.</summary>
    public const string WatchRisk = "vs-dope-overdose-risk";
    public const string WatchSeverity = "vs-dope-overdose-severity";
    public const string WatchRecentDoses = "vs-dope-recent-doses";

    /// <summary>Persistent (server-only) calendar hours of recent doses, oldest first.</summary>
    public const string AttrRecentDoseHours = "vs-dope-recent-dose-hours";
    private const string AttrLastProduct = "vs-dope-last-dose-product";
    private const string ClockKey = "vs-dope-overdose-gamehour";
    private const string EffectKey = "vs-dope-overdose";

    // Keys written by the previous load/threshold model. Removed on join/clear.
    private const string LegacyClockKey = "vs-dope-load-gamehour";
    private static string LegacyLoadKey(string product) => $"vs-dope-load-{product}";

    // ---- Balance -------------------------------------------------------------------------
    /// <summary>Doses (of any drug) inside this many in-game hours stack overdose risk.
    /// At default calendar speed one game hour is two real minutes.</summary>
    public const double RecentDoseWindowGameHours = 2.0;
    private const int MaxTrackedDoses = 32;
    /// <summary>No single roll can be more likely than this.</summary>
    public const float MaxChance = 0.9f;
    /// <summary>Chance multiplier is 1 - tolerance * this. Tolerance caps at 0.75, so at most -30%.</summary>
    public const float ToleranceRiskReduction = 0.4f;
    /// <summary>Severity added per recent dose on top of the drug's base overdose severity.</summary>
    public const float SeverityPerRecentDose = 0.05f;
    public const float SeverityRecoveryPerGameHour = 0.5f;
    public const float DamageSeverityGate = 0.35f;
    public const float DamagePerSecondAtFullSeverity = 0.1f;
    /// <summary>HUD shows a warning once the next dose's chance reaches this.</summary>
    public const float WarningChance = 0.15f;
    private const float MaxSpeedWhileOverdosing = 0.75f;
    private const float SpeedLossAtFullSeverity = 0.6f;
    private const float MinimumSpeed = 0.1f;

    public readonly record struct DrugRisk(float BaseChance, float ChancePerRecentDose, float BaseSeverity);

    /// <summary>Per-drug risk. Heroin is the most dangerous, opium the least.</summary>
    public static readonly IReadOnlyDictionary<string, DrugRisk> Risks = new Dictionary<string, DrugRisk>
    {
        ["opium"] = new(0.005f, 0.015f, 0.30f),
        ["coca-vitae"] = new(0.010f, 0.030f, 0.40f),
        ["morphine"] = new(0.015f, 0.040f, 0.45f),
        ["heroin"] = new(0.020f, 0.050f, 0.55f),
    };

    /// <summary>Uniform [0,1) source for the overdose roll. Replaceable by integration tests.</summary>
    public Func<double> Roll { get; set; } = Random.Shared.NextDouble;

    /// <summary>Pure chance formula, shared by the roll and the HUD risk.</summary>
    public static float ChanceFor(string product, int recentDoses, float tolerance)
    {
        if (!Risks.TryGetValue(product, out var risk)) return 0f;
        if (!float.IsFinite(tolerance)) tolerance = 0f;
        float raw = risk.BaseChance + risk.ChancePerRecentDose * Math.Max(0, recentDoses);
        float reduction = 1f - Math.Clamp(tolerance, 0f, 1f) * ToleranceRiskReduction;
        return Math.Clamp(raw * reduction, 0f, MaxChance);
    }

    public static float SeverityFor(string product, int recentDoses)
        => Risks.TryGetValue(product, out var risk)
            ? Math.Clamp(risk.BaseSeverity + SeverityPerRecentDose * Math.Max(0, recentDoses), 0f, 1f)
            : 0f;

    /// <summary>Record one consumption event. <paramref name="doses"/> above 1 (a big vessel drink)
    /// rolls once per whole dose. Returns true if this event started or worsened an overdose.</summary>
    public bool RecordDose(IPlayer player, string product, double gameHour, float tolerance, float doses = 1f)
    {
        if (!Risks.ContainsKey(product) || !float.IsFinite(doses) || doses <= 0 || player.Entity?.Alive != true) return false;

        Advance(player, gameHour);
        var hours = RecentDoseHours(player).ToList();
        int rolls = Math.Max(1, (int)Math.Round(doses));
        bool overdosed = false;
        for (int i = 0; i < rolls; i++)
        {
            int recent = hours.Count;
            if (Roll() < ChanceFor(product, recent, tolerance))
            {
                Worsen(player, SeverityFor(product, recent));
                overdosed = true;
            }
            hours.Add(gameHour);
        }

        SaveRecentDoseHours(player, hours);
        player.Entity.Attributes.SetString(AttrLastProduct, product);
        Apply(player, 0);
        return overdosed;
    }

    public void Tick(IPlayer player, double gameHour, float seconds)
    {
        if (!player.Entity.Alive) { Clear(player); return; }
        Advance(player, gameHour);
        Apply(player, Math.Clamp(seconds, 0f, 5f));
    }

    // Resume saved state without granting offline recovery. Drops the old load-model keys.
    public void OnJoin(IPlayer player, double gameHour)
    {
        RemoveLegacyKeys(player.Entity);
        player.Entity.Attributes.SetDouble(ClockKey, gameHour);
        if (!player.Entity.Alive) { Clear(player); return; }
        Apply(player, 0);
    }

    public static bool IsOverdosing(IPlayer player) => player.Entity?.WatchedAttributes.GetBool(WatchActive) ?? false;

    public static IReadOnlyList<double> RecentDoseHours(IPlayer player)
        => (player.Entity.Attributes[AttrRecentDoseHours] as DoubleArrayAttribute)?.value ?? Array.Empty<double>();

    private static void SaveRecentDoseHours(IPlayer player, List<double> hours)
    {
        if (hours.Count > MaxTrackedDoses) hours.RemoveRange(0, hours.Count - MaxTrackedDoses);
        if (hours.Count == 0) player.Entity.Attributes.RemoveAttribute(AttrRecentDoseHours);
        else player.Entity.Attributes.SetAttribute(AttrRecentDoseHours, new DoubleArrayAttribute(hours.ToArray()));
    }

    private static void Worsen(IPlayer player, float addedSeverity)
    {
        var watched = player.Entity.WatchedAttributes;
        float current = watched.GetBool(WatchActive) ? watched.GetFloat(WatchSeverity) : 0f;
        watched.SetFloat(WatchSeverity, Math.Clamp(current + addedSeverity, 0f, 1f));
        watched.SetBool(WatchActive, true);
    }

    // Prune the dose window and recover severity by elapsed calendar time.
    private static void Advance(IPlayer player, double gameHour)
    {
        var attrs = player.Entity.Attributes;
        double previous = attrs.GetDouble(ClockKey, gameHour);
        double elapsed = Math.Max(0, gameHour - previous);
        attrs.SetDouble(ClockKey, gameHour);

        var hours = RecentDoseHours(player);
        if (hours.Count > 0 && hours[0] <= gameHour - RecentDoseWindowGameHours)
            SaveRecentDoseHours(player, hours.Where(h => h > gameHour - RecentDoseWindowGameHours).ToList());

        var watched = player.Entity.WatchedAttributes;
        if (!watched.GetBool(WatchActive)) return;
        float severity = watched.GetFloat(WatchSeverity);
        if (!float.IsFinite(severity)) severity = 0f;
        severity -= (float)elapsed * SeverityRecoveryPerGameHour;
        if (severity <= 0f)
        {
            watched.SetBool(WatchActive, false);
            watched.SetFloat(WatchSeverity, 0f);
        }
        else watched.SetFloat(WatchSeverity, Math.Min(1f, severity));
    }

    // Publish next-dose risk and apply the overdose slow/damage.
    private static void Apply(IPlayer player, float seconds)
    {
        var entity = player.Entity;
        var watched = entity.WatchedAttributes;
        int recent = RecentDoseHours(player).Count;
        string product = entity.Attributes.GetString(AttrLastProduct) ?? "";
        float risk = recent == 0 ? 0f : ChanceFor(product, recent, entity.Attributes.GetFloat(AddictionSystem.ToleranceKey(product)));
        watched.SetFloat(WatchRisk, risk);
        watched.SetInt(WatchRecentDoses, recent);

        bool active = watched.GetBool(WatchActive);
        float severity = active ? Math.Clamp(watched.GetFloat(WatchSeverity), 0f, 1f) : 0f;
        entity.Stats.Remove("walkspeed", EffectKey);
        if (!active) return;

        // Apply once, after other modifiers. Stimulants cannot cancel the slow,
        // and combining opioid modifiers cannot produce negative/reversed movement.
        float speed = entity.Stats.GetBlended("walkspeed");
        float target = Math.Max(MinimumSpeed, Math.Min(speed, MaxSpeedWhileOverdosing - SpeedLossAtFullSeverity * severity));
        entity.Stats.Set("walkspeed", EffectKey, target - speed);

        if (severity > DamageSeverityGate && seconds > 0)
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison },
                severity * DamagePerSecondAtFullSeverity * seconds);
    }

    public void Clear(IPlayer player)
    {
        var entity = player.Entity;
        RemoveLegacyKeys(entity);
        entity.Attributes.RemoveAttribute(ClockKey);
        entity.Attributes.RemoveAttribute(AttrRecentDoseHours);
        entity.Attributes.RemoveAttribute(AttrLastProduct);
        entity.WatchedAttributes.SetBool(WatchActive, false);
        entity.WatchedAttributes.SetFloat(WatchRisk, 0f);
        entity.WatchedAttributes.SetFloat(WatchSeverity, 0f);
        entity.WatchedAttributes.SetInt(WatchRecentDoses, 0);
        entity.Stats.Remove("walkspeed", EffectKey);
    }

    private static void RemoveLegacyKeys(EntityPlayer entity)
    {
        foreach (string product in Risks.Keys) entity.WatchedAttributes.RemoveAttribute(LegacyLoadKey(product));
        entity.Attributes.RemoveAttribute(LegacyClockKey);
    }
}
