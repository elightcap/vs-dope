using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsDope.Systems;

/// <summary>A vanilla player stat modifier. EntityStats blend additively: 1 + sum of modifiers.</summary>
public readonly record struct StatModifier(string Stat, float Value);

/// <summary>What one product does to vanilla stats while active, and for how long.</summary>
public sealed record DrugStatProfile(double DurationHours, StatModifier[] Stats, float DamageMitigation = 0f);

/// <summary>
/// Calendar-timed vanilla stat effects per product (issue #55). Doses refresh one timer per
/// product and never stack. Values are scaled by the tolerance multiplier at dose time; the
/// multiplier is stored so relogs restore the same values.
/// </summary>
public sealed class DrugStatEffectSystem : ModSystem
{
    public const string KeyPrefix = "vs-dope-stat-";
    public const string CocaProduct = "coca-vitae";
    public const string CocaCrashProduct = "coca-crash";
    /// <summary>One-off satiety cost when the coca high turns into the crash.</summary>
    public const float CocaCrashSaturationCost = 150f;

    // Stat codes as registered by EntityPlayer in 1.22.7 ("healingeffectivness" is the vanilla spelling).
    public const string MiningSpeed = "miningSpeedMul";
    public const string HungerRate = "hungerrate";
    public const string HealingEffectiveness = "healingeffectivness";
    public const string RangedAccuracy = "rangedWeaponsAcc";
    public const string AnimalSeekingRange = "animalSeekingRange";
    public const string WalkSpeed = "walkspeed";

    public static readonly IReadOnlyDictionary<string, DrugStatProfile> Profiles = new Dictionary<string, DrugStatProfile>
    {
        // Stimulant: dig faster and eat less, then pay for it.
        [CocaProduct] = new(1.0, new[] { new StatModifier(MiningSpeed, 0.40f), new StatModifier(HungerRate, -0.30f) }),
        [CocaCrashProduct] = new(1.0, new[] { new StatModifier(HungerRate, 0.40f), new StatModifier(WalkSpeed, -0.15f) }),
        // Opioids: healing items work better and pain is dulled, but aim suffers.
        ["opium"] = new(1.0, new[] { new StatModifier(HealingEffectiveness, 0.20f), new StatModifier(RangedAccuracy, -0.20f) }, 0.10f),
        ["morphine"] = new(1.5, new[] { new StatModifier(HealingEffectiveness, 0.35f), new StatModifier(RangedAccuracy, -0.35f) }, 0.20f),
        ["heroin"] = new(2.0, new[] { new StatModifier(HealingEffectiveness, 0.50f), new StatModifier(RangedAccuracy, -0.50f) }, 0.30f),
    };

    /// <summary>Marijuana stats, owned by <see cref="StonedSystem"/>'s lifecycle. Values below 1 on
    /// animalSeekingRange shrink how close mobs must be to notice you; above 1 has no effect.</summary>
    public static readonly StatModifier[] StonedStats =
    {
        new(HungerRate, 0.30f),
        new(AnimalSeekingRange, -0.35f),
    };

    private readonly ConditionalWeakTable<Entity, object> hookedHealth = new();
    private ICoreServerAPI? sapi;
    private long tickId;

    public static string StatKey(string product) => KeyPrefix + product;
    public static string ExpiryKey(string product) => StatKey(product) + "-expires-gamehour";
    private static string MultiplierKey(string product) => StatKey(product) + "-mult";

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Event.PlayerNowPlaying += OnPlaying;
        api.Event.PlayerRespawn += OnRespawn;
        api.Event.PlayerDeath += OnDeath;
        tickId = api.Event.RegisterGameTickListener(OnTick, 1000);
    }

    public static bool IsActive(Entity entity, string product, double now) =>
        entity.WatchedAttributes.GetDouble(ExpiryKey(product)) > now;

    public static void Apply(EntityAgent entity, string product, float effectMultiplier, double now)
    {
        if (entity.World.Side != EnumAppSide.Server || !entity.Alive) return;
        if (!Profiles.TryGetValue(product, out var profile)) return;
        // A fresh high replaces a running crash instead of stacking against it.
        if (product == CocaProduct) End(entity, CocaCrashProduct);
        Start(entity, product, profile, effectMultiplier, now);
    }

    public static void SetStats(Entity entity, string key, IEnumerable<StatModifier> stats, float multiplier)
    {
        foreach (var stat in stats) entity.Stats.Set(stat.Stat, key, stat.Value * multiplier, true);
    }

    public static void RemoveStats(Entity entity, string key, IEnumerable<StatModifier> stats)
    {
        foreach (var stat in stats) entity.Stats.Remove(stat.Stat, key);
    }

    /// <summary>Ends expired effects; an expired coca high starts the crash.</summary>
    public static void Tick(EntityAgent entity, double now)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        if (!entity.Alive) { Clear(entity); return; }
        foreach (string product in Profiles.Keys)
        {
            double expiry = entity.WatchedAttributes.GetDouble(ExpiryKey(product));
            if (expiry <= 0 || now < expiry) continue;
            float multiplier = entity.Attributes.GetFloat(MultiplierKey(product), 1f);
            End(entity, product);
            if (product != CocaProduct) continue;
            // The crash starts when the high ended, so late ticks and relogs do not extend it.
            Start(entity, CocaCrashProduct, Profiles[CocaCrashProduct], multiplier, expiry);
            entity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(CocaCrashSaturationCost * multiplier);
            if (now >= expiry + Profiles[CocaCrashProduct].DurationHours) End(entity, CocaCrashProduct);
        }
    }

    /// <summary>Restores unexpired effects with their dose-time strength.</summary>
    public static void Resume(EntityAgent entity, double now)
    {
        foreach (var (product, profile) in Profiles)
            if (IsActive(entity, product, now))
                SetStats(entity, StatKey(product), profile.Stats, entity.Attributes.GetFloat(MultiplierKey(product), 1f));
        Tick(entity, now);
    }

    public static void Clear(Entity entity)
    {
        foreach (string product in Profiles.Keys) End(entity, product);
    }

    /// <summary>Strongest active opioid mitigation. Heals and our own internal poison
    /// (overdose, withdrawal) are never reduced.</summary>
    public static float Mitigate(Entity entity, float damage, DamageSource source, double now)
    {
        if (damage <= 0 || source.Type == EnumDamageType.Heal || source.Source == EnumDamageSource.Internal) return damage;
        float mitigation = 0f;
        foreach (var (product, profile) in Profiles)
            if (profile.DamageMitigation > 0 && IsActive(entity, product, now))
                mitigation = Math.Max(mitigation, profile.DamageMitigation * entity.Attributes.GetFloat(MultiplierKey(product), 1f));
        return damage * (1f - Math.Clamp(mitigation, 0f, 0.9f));
    }

    private static void Start(Entity entity, string product, DrugStatProfile profile, float multiplier, double from)
    {
        SetStats(entity, StatKey(product), profile.Stats, multiplier);
        entity.WatchedAttributes.SetDouble(ExpiryKey(product), from + profile.DurationHours);
        entity.Attributes.SetFloat(MultiplierKey(product), multiplier);
    }

    private static void End(Entity entity, string product)
    {
        RemoveStats(entity, StatKey(product), Profiles[product].Stats);
        if (entity.WatchedAttributes.HasAttribute(ExpiryKey(product))) entity.WatchedAttributes.RemoveAttribute(ExpiryKey(product));
        entity.Attributes.RemoveAttribute(MultiplierKey(product));
    }

    private void HookHealth(EntityPlayer entity)
    {
        // Respawn can keep the same entity object, so hook each entity once.
        if (hookedHealth.TryGetValue(entity, out _)) return;
        var health = entity.GetBehavior<EntityBehaviorHealth>();
        if (health == null) return;
        health.onDamaged += (damage, source) => Mitigate(entity, damage, source, sapi!.World.Calendar.TotalHours);
        hookedHealth.Add(entity, health);
    }

    private void OnTick(float dt)
    {
        foreach (var player in sapi!.World.AllOnlinePlayers)
            if (player is IServerPlayer serverPlayer && serverPlayer.ConnectionState == EnumClientState.Playing && player.Entity != null)
                Tick(player.Entity, sapi.World.Calendar.TotalHours);
    }

    private void OnPlaying(IServerPlayer player)
    {
        if (player.Entity == null) return;
        HookHealth(player.Entity);
        Resume(player.Entity, sapi!.World.Calendar.TotalHours);
    }

    private void OnRespawn(IServerPlayer player)
    {
        if (player.Entity == null) return;
        Clear(player.Entity);
        HookHealth(player.Entity);
    }

    private void OnDeath(IServerPlayer player, DamageSource source)
    {
        if (player.Entity != null) Clear(player.Entity);
    }

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.UnregisterGameTickListener(tickId);
            sapi.Event.PlayerNowPlaying -= OnPlaying;
            sapi.Event.PlayerRespawn -= OnRespawn;
            sapi.Event.PlayerDeath -= OnDeath;
        }
        base.Dispose();
    }
}
