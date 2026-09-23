using System;
using Vintagestory.API.Common;

namespace VsDope.Systems;

/// <summary>Server-owned drug load. These are game balance units, independent of visual intoxication.</summary>
public sealed class OverdoseSystem
{
    public const string WatchActive = "vs-dope-overdose";
    public const string WatchRisk = "vs-dope-overdose-risk";
    public const string WatchSeverity = "vs-dope-overdose-severity";
    private const string ClockKey = "vs-dope-load-gamehour";
    private const string EffectKey = "vs-dope-overdose";
    private static readonly string[] Products = { "opium", "morphine", "heroin", "coca-vitae" };
    private const float MetabolismPerGameHour = 5f;
    private const float DamagePerSecond = 0.2f;

    public static string LoadKey(string product) => $"vs-dope-load-{product}";

    public void RecordDose(IPlayer player, string product, double gameHour, float doses = 1f)
    {
        float doseLoad = product switch
        {
            "opium" => 3f,
            "morphine" => 6f,
            "heroin" => 5f,
            "coca-vitae" => 5f,
            _ => 0f
        };
        if (doseLoad == 0 || !float.IsFinite(doses) || doses <= 0 || !player.Entity.Alive) return;

        Advance(player, gameHour);
        var watched = player.Entity.WatchedAttributes;
        string key = LoadKey(product);
        watched.SetFloat(key, Math.Min(1000f, ReadLoad(player, product) + doseLoad * doses));
        Refresh(player, 0);
    }

    public void Tick(IPlayer player, double gameHour, float seconds)
    {
        if (!player.Entity.Alive) { Clear(player); return; }
        Advance(player, gameHour);
        Refresh(player, Math.Clamp(seconds, 0f, 5f));
    }

    // Resume the saved load without granting offline metabolism or generating a dose.
    public void OnJoin(IPlayer player, double gameHour)
    {
        player.Entity.Attributes.SetDouble(ClockKey, gameHour);
        if (!player.Entity.Alive) { Clear(player); return; }
        Refresh(player, 0);
    }

    private static float ReadLoad(IPlayer player, string product)
    {
        float value = player.Entity.WatchedAttributes.GetFloat(LoadKey(product));
        return float.IsFinite(value) ? Math.Clamp(value, 0f, 1000f) : 0f;
    }

    private static void Advance(IPlayer player, double gameHour)
    {
        var attrs = player.Entity.Attributes;
        double previous = attrs.GetDouble(ClockKey, gameHour);
        double elapsed = Math.Max(0, gameHour - previous);
        attrs.SetDouble(ClockKey, gameHour);
        foreach (string product in Products)
        {
            float load = Math.Max(0f, ReadLoad(player, product) - (float)elapsed * MetabolismPerGameHour);
            if (load == 0) player.Entity.WatchedAttributes.RemoveAttribute(LoadKey(product));
            else player.Entity.WatchedAttributes.SetFloat(LoadKey(product), load);
        }
    }

    // Sum normalized exposure so changing products cannot bypass overdose.
    // Tolerance raises each threshold from 15 to at most 25 load units.
    private static void Refresh(IPlayer player, float seconds)
    {
        var entity = player.Entity;
        float risk = 0f;
        foreach (string product in Products)
        {
            float tolerance = entity.Attributes.GetFloat($"vs-dope-tolerance-{product}");
            if (!float.IsFinite(tolerance)) tolerance = 0;
            float threshold = 15f + Math.Clamp(tolerance, 0f, 0.5f) * 20f;
            risk += ReadLoad(player, product) / threshold;
        }

        bool active = risk > 1f;
        float severity = Math.Clamp(risk - 1f, 0f, 1f);
        entity.WatchedAttributes.SetFloat(WatchRisk, risk);
        entity.WatchedAttributes.SetFloat(WatchSeverity, severity);
        entity.WatchedAttributes.SetBool(WatchActive, active);
        entity.Stats.Remove("walkspeed", EffectKey);
        if (!active) return;

        // Apply once, after other modifiers. Stimulants cannot cancel the slow,
        // and combining opioid modifiers cannot produce negative/reversed movement.
        float speed = entity.Stats.GetBlended("walkspeed");
        float target = Math.Max(0.1f, Math.Min(speed, 0.75f - 0.6f * severity));
        entity.Stats.Set("walkspeed", EffectKey, target - speed);

        if (severity > 0.35f && seconds > 0)
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison },
                severity * DamagePerSecond * seconds);
    }

    public void Clear(IPlayer player)
    {
        var entity = player.Entity;
        foreach (string product in Products) entity.WatchedAttributes.RemoveAttribute(LoadKey(product));
        entity.Attributes.RemoveAttribute(ClockKey);
        entity.WatchedAttributes.SetBool(WatchActive, false);
        entity.WatchedAttributes.SetFloat(WatchRisk, 0f);
        entity.WatchedAttributes.SetFloat(WatchSeverity, 0f);
        entity.Stats.Remove("walkspeed", EffectKey);
    }
}
