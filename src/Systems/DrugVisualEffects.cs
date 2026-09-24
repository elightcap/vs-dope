using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

namespace VsDope.Systems;

/// <summary>
/// Per-drug screen effects. Vintage Story drives two perception effects from watched attributes:
/// "intoxication" (drunk sway, head bob, bloom) and "psychedelic" (colour warp, intensity = value / 2).
/// Vanilla food and drink cap them at 1.1 and 2.0, and EntityBehaviorHunger.detox fades both by
/// 0.005 per real second at default calendar speed (about 0.6 per in-game hour). We use the same
/// caps and let vanilla detox fade the effect so doses feel like vanilla alcohol or mushrooms.
/// </summary>
public static class DrugVisualEffects
{
    public const float IntoxicationCap = 1.1f;
    public const float PsychedelicCap = 2f;

    public readonly record struct Strength(float Intoxication, float Psychedelic);

    /// <summary>Added per dose (one item, or 0.1 L of heroin / morphine solution), before tolerance.
    /// Tiered weakest to strongest: coca vitae, opium, morphine, heroin.</summary>
    public static readonly IReadOnlyDictionary<string, Strength> PerDose = new Dictionary<string, Strength>
    {
        ["coca-vitae"] = new(0.05f, 0.15f),
        ["opium"] = new(0.15f, 0.10f),
        ["morphine"] = new(0.25f, 0.20f),
        ["heroin"] = new(0.35f, 0.40f),
    };

    /// <summary>Adds the product's visual strength scaled by dose count and tolerance multiplier.</summary>
    public static void Apply(Entity entity, string product, float doses, float effectMultiplier)
    {
        if (!PerDose.TryGetValue(product, out var strength) || !float.IsFinite(doses) || doses <= 0) return;
        float scale = doses * Math.Clamp(effectMultiplier, 0f, 1f);
        Add(entity, "intoxication", strength.Intoxication * scale, IntoxicationCap);
        Add(entity, "psychedelic", strength.Psychedelic * scale, PsychedelicCap);
    }

    private static void Add(Entity entity, string key, float amount, float cap)
    {
        if (amount <= 0) return;
        float current = entity.WatchedAttributes.GetFloat(key);
        if (!float.IsFinite(current)) current = 0f;
        // Never raise a value above the cap, but don't cut down a higher value set elsewhere either.
        if (current >= cap) return;
        entity.WatchedAttributes.SetFloat(key, Math.Min(cap, current + amount));
    }
}
