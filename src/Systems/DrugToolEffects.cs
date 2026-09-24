using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope.Items;

namespace VsDope.Systems;

/// <summary>Persistent calendar timers for practical drug effects and coca's comedown.</summary>
public sealed class DrugToolEffects : ModSystem
{
    public const double CocaHours = 1;
    public const double OpiumHours = 0.5;
    public const double MorphineHours = 1;
    public const double CrashHours = 0.5;
    public const float CocaMining = 0.25f;
    public const float CocaHunger = -0.2f;
    public const float OpiumHealing = 0.15f;
    public const float MorphineHealing = 0.3f;
    public const float OpiumAccuracy = -0.15f;
    public const float MorphineAccuracy = -0.3f;
    public const float OpiumDetection = 0.15f;
    public const float MorphineDetection = 0.25f;
    public const float OpiumResistance = 0.1f;
    public const float MorphineResistance = 0.2f;
    public const float CrashSpeed = -0.2f;
    public const float CrashSatiety = 150;
    public const string CrashKey = "vs-dope-coca-crash";
    public const string CrashExpiryKey = CrashKey + "-expires-gamehour";
    private const string CrashStrengthKey = CrashKey + "-strength";
    private const string HarmonyId = "vs-dope.drug-tool-effects";
    private static readonly string[] Products = { "coca-vitae", "opium", "morphine" };
    private static readonly string[] StatCodes = { "miningSpeedMul", "hungerrate", "healingeffectivness", "rangedWeaponsAcc", "animalSeekingRange" };
    private ICoreServerAPI? sapi;
    private Harmony? harmony;
    private long tickId;

    public static string EffectKey(string product) => "vs-dope-tool-" + product;
    public static string ExpiryKey(string product) => EffectKey(product) + "-expires-gamehour";
    private static string StrengthKey(string product) => EffectKey(product) + "-strength";

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        harmony = new Harmony(HarmonyId);
        // Verified in 1.22.7: health has no general damage-resistance stat. This hook
        // adjusts physical entity attacks and scheduled healing, never overdose poison.
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorHealth), nameof(EntityBehaviorHealth.OnEntityReceiveDamage)),
            prefix: new HarmonyMethod(typeof(DrugToolEffects), nameof(BeforeDamage)));
        api.Event.PlayerNowPlaying += OnPlaying;
        api.Event.PlayerDeath += OnDeath;
        api.Event.PlayerRespawn += OnRespawn;
        tickId = api.Event.RegisterGameTickListener(OnTick, 250);
    }

    public static void Apply(EntityAgent entity, string product, float strength)
    {
        if (entity.World.Side != EnumAppSide.Server || !entity.Alive || !Products.Contains(product)) return;
        double now = entity.World.Calendar.TotalHours;
        // Settle an expired dose before a fresh dose replaces its expiry.
        Tick(entity, now);
        strength = SanitizeStrength(strength);
        double hours = product == "coca-vitae" ? CocaHours : product == "opium" ? OpiumHours : MorphineHours;
        entity.WatchedAttributes.SetDouble(ExpiryKey(product), now + hours);
        entity.WatchedAttributes.SetFloat(StrengthKey(product), strength);
        ApplyStats(entity, product, strength);
    }

    public static float ActiveStrength(Entity entity, string product, double now)
    {
        double expiry = entity.WatchedAttributes.GetDouble(ExpiryKey(product));
        return entity.Alive && double.IsFinite(expiry) && expiry > now
            ? SanitizeStrength(entity.WatchedAttributes.GetFloat(StrengthKey(product), 1)) : 0;
    }

    public static void Tick(EntityAgent entity, double now)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        if (!entity.Alive) { Clear(entity); return; }
        foreach (string product in Products)
        {
            if (!entity.WatchedAttributes.HasAttribute(ExpiryKey(product))) continue;
            double expiry = entity.WatchedAttributes.GetDouble(ExpiryKey(product));
            if (double.IsFinite(expiry) && now < expiry) continue;
            float strength = SanitizeStrength(entity.WatchedAttributes.GetFloat(StrengthKey(product), 1));
            ClearProduct(entity, product);
            if (product == "coca-vitae")
            {
                RemoveCocaSpeed(entity);
                if (double.IsFinite(expiry) && expiry > 0)
                {
                    // Clearing the active record first makes the satiety charge single-use,
                    // including repeat ticks, reapplication, and reconnect after expiry.
                    var hunger = entity.GetBehavior<EntityBehaviorHunger>();
                    if (hunger != null) hunger.Saturation = Math.Max(0, hunger.Saturation - CrashSatiety * strength);
                    entity.WatchedAttributes.SetDouble(CrashExpiryKey, expiry + CrashHours);
                    entity.WatchedAttributes.SetFloat(CrashStrengthKey, strength);
                    entity.Stats.Set("walkspeed", CrashKey, CrashSpeed * strength, true);
                }
            }
        }
        double crashExpiry = entity.WatchedAttributes.GetDouble(CrashExpiryKey);
        if (entity.WatchedAttributes.HasAttribute(CrashExpiryKey) && (!double.IsFinite(crashExpiry) || now >= crashExpiry))
            ClearCrash(entity);
    }

    public static void Resume(EntityAgent entity, double now)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        Tick(entity, now);
        if (!entity.Alive) return;
        foreach (string product in Products)
        {
            float strength = ActiveStrength(entity, product, now);
            if (strength <= 0) { ClearProduct(entity, product); continue; }
            ApplyStats(entity, product, strength);
            if (product == "coca-vitae")
            {
                // The legacy item callback isn't restored on login. Keep the HUD's
                // established key and restore speed from the same original dose strength.
                string speedKey = DrugConsumableItem.SpeedEffectKeyFor(product);
                entity.Stats.Set("walkspeed", speedKey, strength);
                entity.WatchedAttributes.SetDouble(speedKey + "-expires-gamehour", entity.WatchedAttributes.GetDouble(ExpiryKey(product)));
            }
        }
        if (entity.WatchedAttributes.GetDouble(CrashExpiryKey) > now)
            entity.Stats.Set("walkspeed", CrashKey, CrashSpeed * SanitizeStrength(entity.WatchedAttributes.GetFloat(CrashStrengthKey, 1)), true);
        else ClearCrash(entity);
    }

    private static void ApplyStats(EntityAgent entity, string product, float strength)
    {
        string key = EffectKey(product);
        if (product == "coca-vitae")
        {
            entity.Stats.Set("miningSpeedMul", key, CocaMining * strength, true);
            entity.Stats.Set("hungerrate", key, CocaHunger * strength, true);
        }
        else
        {
            bool morphine = product == "morphine";
            entity.Stats.Set("healingeffectivness", key, (morphine ? MorphineHealing : OpiumHealing) * strength, true);
            entity.Stats.Set("rangedWeaponsAcc", key, (morphine ? MorphineAccuracy : OpiumAccuracy) * strength, true);
            entity.Stats.Set("animalSeekingRange", key, (morphine ? MorphineDetection : OpiumDetection) * strength, true);
        }
    }

    private static void BeforeDamage(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
    {
        Entity entity = __instance.entity;
        if (entity is not EntityPlayer || entity.World.Side != EnumAppSide.Server || !entity.Alive || damage <= 0) return;
        double now = entity.World.Calendar.TotalHours;
        if (damageSource.Type == EnumDamageType.Heal)
        {
            // 1.22.7 HealingItem bandages/poultices schedule healing without reading
            // healingeffectivness. Scale our bonus once when scheduling, never again
            // on each delivered tick. Legacy instant ItemPoultice reads the stat itself.
            if (damageSource.Source == EnumDamageSource.Internal && damageSource.Duration > TimeSpan.Zero)
                damage *= 1 + OpiumHealing * ActiveStrength(entity, "opium", now)
                    + MorphineHealing * ActiveStrength(entity, "morphine", now);
            return;
        }
        // A scheduled damage-over-time hit re-enters this method for each actual
        // tick. Scale those ticks, not the scheduling call as well.
        if (damageSource.Duration > TimeSpan.Zero) return;
        if (damageSource.Source is not (EnumDamageSource.Entity or EnumDamageSource.Player)
            || damageSource.Type is not (EnumDamageType.BluntAttack or EnumDamageType.SlashingAttack or EnumDamageType.PiercingAttack)) return;
        // Combining opiates takes the strongest protection, not a stack toward immunity.
        float resistance = Math.Max(OpiumResistance * ActiveStrength(entity, "opium", now),
            MorphineResistance * ActiveStrength(entity, "morphine", now));
        damage *= 1 - resistance;
    }

    private static float SanitizeStrength(float strength) => float.IsFinite(strength) ? Math.Clamp(strength, 0, 1) : 0;

    private static void ClearProduct(EntityAgent entity, string product)
    {
        foreach (string stat in StatCodes) entity.Stats.Remove(stat, EffectKey(product));
        entity.WatchedAttributes.RemoveAttribute(ExpiryKey(product));
        entity.WatchedAttributes.RemoveAttribute(StrengthKey(product));
    }

    private static void RemoveCocaSpeed(EntityAgent entity)
    {
        string key = DrugConsumableItem.SpeedEffectKeyFor("coca-vitae");
        entity.Stats.Remove("walkspeed", key);
        entity.WatchedAttributes.RemoveAttribute(key + "-expires");
        entity.WatchedAttributes.RemoveAttribute(key + "-expires-gamehour");
    }

    private static void ClearCrash(EntityAgent entity)
    {
        entity.Stats.Remove("walkspeed", CrashKey);
        entity.WatchedAttributes.RemoveAttribute(CrashExpiryKey);
        entity.WatchedAttributes.RemoveAttribute(CrashStrengthKey);
    }

    public static void Clear(EntityAgent entity)
    {
        foreach (string product in Products) ClearProduct(entity, product);
        RemoveCocaSpeed(entity);
        ClearCrash(entity);
    }

    private void OnTick(float dt)
    {
        foreach (var player in sapi!.World.AllOnlinePlayers)
            if (player is IServerPlayer serverPlayer && serverPlayer.ConnectionState == EnumClientState.Playing && player.Entity != null)
                Tick(player.Entity, sapi.World.Calendar.TotalHours);
    }
    private void OnPlaying(IServerPlayer player) => Resume(player.Entity, sapi!.World.Calendar.TotalHours);
    private void OnDeath(IServerPlayer player, DamageSource source) => Clear(player.Entity);
    private void OnRespawn(IServerPlayer player) => Clear(player.Entity);

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.UnregisterGameTickListener(tickId);
            sapi.Event.PlayerNowPlaying -= OnPlaying;
            sapi.Event.PlayerDeath -= OnDeath;
            sapi.Event.PlayerRespawn -= OnRespawn;
        }
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }
}
