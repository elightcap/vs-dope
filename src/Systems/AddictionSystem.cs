using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsDope.Items;

namespace VsDope.Systems;

public class AddictionSystem : IDisposable
{
    private const string AttrAddictionLevel = "vs-dope-addiction-level";
    private const string AttrLastUseDay = "vs-dope-last-use-day";
    private const string AttrDaysUsedConsecutively = "vs-dope-days-used";
    private const string AttrWithdrawalStartDay = "vs-dope-withdrawal-start";

    public const string WatchAddictionLevel = "vs-dope-addiction-level";
    public const string WatchDaysUsed = "vs-dope-days-used";
    public const string WatchWithdrawal = "vs-dope-withdrawal";

    private const int AddictionThreshold = 5;
    private const float WithdrawalSeverityBase = 0.3f;
    private const int DaysPerAddictionDecay = 2;
    private const float HeroinSlowFactor = 0.5f;
    private const double HeroinSpeedDurationGameHours = 1.0;
    private const string HeroinSpeedEffectKey = "vs-dope-heroin-slow";
    private const string HeroinSpeedExpiryKey = "vs-dope-heroin-slow-expires-gamehour";
    /// <summary>One syringe application; also the unit for vessel drinking.</summary>
    public const float HeroinLitresPerDose = 0.1f;
    private const float HeroinHealPerLitre = 24f;
    private const string WithdrawalEffectKey = "vs-dope-withdrawal";
    private const float WithdrawalSlowPerSeverity = 0.4f;
    private const float WithdrawalDamageSeverityGate = 0.8f;
    private const float WithdrawalDamagePerSeverity = 1.5f;

    // Walkspeed modifiers from a single dose; cleared on death/respawn.
    private static readonly string[] AcuteSpeedEffectKeys =
    {
        HeroinSpeedEffectKey,
        DrugConsumableItem.SpeedEffectKeyFor("opium"),
        DrugConsumableItem.SpeedEffectKeyFor("morphine"),
        DrugConsumableItem.SpeedEffectKeyFor("coca-vitae"),
    };

    public OverdoseSystem Overdose { get; } = new();

    // Tolerance is per finished product. The first two uses in an in-game day do not
    // increase tolerance. Heavy same-day use does, and days away from that product recover it.
    private const int FreeUsesPerDay = 2;
    private const float TolerancePerExcessUse = 0.08f;
    private const float MaxTolerance = 0.75f;
    private const float ToleranceRecoveryPerUnusedDay = 0.18f;
    private const float MinimumEffectMultiplier = 0.25f;

    private ICoreServerAPI api = null!;
    private long lastProcessedGameHour = -1;
    private long tickId;

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        api.Event.PlayerRespawn += OnPlayerRespawn;
        api.Event.PlayerDeath += OnPlayerDeath;
        lastProcessedGameHour = -1;
        tickId = api.Event.RegisterGameTickListener(OnTick, 1000);
    }

    public static string ToleranceKey(string product) => $"vs-dope-tolerance-{product}";
    private static string TolDayKey(string product) => $"vs-dope-tolerance-day-{product}";
    private static string TolUsesKey(string product) => $"vs-dope-tolerance-uses-{product}";

    public float GetEffectMultiplier(IPlayer player, string product)
    {
        RecoverTolerance(player, product);
        float tolerance = Math.Clamp(player.Entity.Attributes.GetFloat(ToleranceKey(product)), 0f, MaxTolerance);
        return Math.Max(MinimumEffectMultiplier, 1f - tolerance);
    }

    private void RecordToleranceUse(IPlayer player, string product)
    {
        RecoverTolerance(player, product);
        var attrs = player.Entity.Attributes;
        int today = (int)api.World.Calendar.TotalDays;
        int storedDay = attrs.GetInt(TolDayKey(product));
        int uses = storedDay == today ? attrs.GetInt(TolUsesKey(product)) : 0;
        uses++;
        attrs.SetInt(TolDayKey(product), today);
        attrs.SetInt(TolUsesKey(product), uses);

        if (uses > FreeUsesPerDay)
        {
            float tolerance = attrs.GetFloat(ToleranceKey(product));
            float bingeScale = 1f + Math.Min(1.5f, (uses - FreeUsesPerDay - 1) * 0.15f);
            attrs.SetFloat(ToleranceKey(product), Math.Min(MaxTolerance, tolerance + TolerancePerExcessUse * bingeScale));
        }
    }

    private void RecoverTolerance(IPlayer player, string product)
    {
        var attrs = player.Entity.Attributes;
        int today = (int)api.World.Calendar.TotalDays;
        int lastDay = attrs.GetInt(TolDayKey(product));
        if (lastDay <= 0 || lastDay >= today) return;

        int unusedDays = today - lastDay;
        float tolerance = Math.Max(0f, attrs.GetFloat(ToleranceKey(product)) - unusedDays * ToleranceRecoveryPerUnusedDay);
        attrs.SetFloat(ToleranceKey(product), tolerance);
        attrs.SetInt(TolDayKey(product), today);
        attrs.SetInt(TolUsesKey(product), 0);
    }

    private void OnTick(float dt)
    {
        if (api.World?.Calendar == null) return;
        foreach (var player in api.Server.Players.Where(p => p.ConnectionState == EnumClientState.Playing))
        {
            if (player.Entity == null) continue;
            ExpireHeroinEffect(player);
            Overdose.Tick(player, api.World.Calendar.TotalHours, dt);
        }

        // Drive progression from the in-game clock, not real time: TotalHours only
        // advances with calendar ticks, so paused/offline time counts nothing.
        long currentGameHour = (long)api.World.Calendar.TotalHours;
        if (lastProcessedGameHour < 0)
        {
            lastProcessedGameHour = currentGameHour;
            return;
        }

        int elapsed = (int)Math.Min(currentGameHour - lastProcessedGameHour, 24);
        for (int i = 0; i < elapsed; i++)
        {
            lastProcessedGameHour++;
            if (api.World.Calendar.FullHourOfDay == 0) ProcessAllOnlinePlayers();
        }
    }

    private void ExpireHeroinEffect(IPlayer player)
    {
        var entity = player.Entity;
        double expiry = entity.WatchedAttributes.GetDouble(HeroinSpeedExpiryKey);
        if (expiry > 0 && api.World.Calendar.TotalHours >= expiry)
        {
            entity.Stats.Remove("walkspeed", HeroinSpeedEffectKey);
            entity.WatchedAttributes.RemoveAttribute(HeroinSpeedExpiryKey);
        }
    }

    public void ApplyHeroinSyringeDose(IPlayer player) => ApplyHeroinDose(player, HeroinLitresPerDose);

    // Both injection and native vessel drinking report actual consumed volume here.
    // Never infer consumption from psychedelic levels: they decay, cap, and survive relogs.
    public void ApplyHeroinDose(IPlayer player, float litres)
    {
        if (!float.IsFinite(litres) || litres <= 0 || !player.Entity.Alive) return;
        var entity = player.Entity;
        float multiplier = GetEffectMultiplier(player, "heroin");
        float doses = litres / HeroinLitresPerDose;
        entity.Stats.Set("walkspeed", HeroinSpeedEffectKey, -HeroinSlowFactor * multiplier);
        entity.WatchedAttributes.SetDouble(HeroinSpeedExpiryKey, api.World.Calendar.TotalHours + HeroinSpeedDurationGameHours);
        RecordDrugDose(player, "heroin", doses);
        if (!IsOverdosing(player))
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, HeroinHealPerLitre * litres * multiplier);
        DrugVisualEffects.Apply(entity, "heroin", doses, multiplier);
    }

    /// <summary>The single entry point for every consumption route (items, syringes, vessels).
    /// Rolls the overdose chance against the tolerance the player had *before* this dose,
    /// then records addiction and tolerance.</summary>
    public void RecordDrugDose(IPlayer player, string product, float doses = 1f)
    {
        RecoverTolerance(player, product);
        float tolerance = Math.Clamp(player.Entity.Attributes.GetFloat(ToleranceKey(product)), 0f, MaxTolerance);
        Overdose.RecordDose(player, product, api.World.Calendar.TotalHours, tolerance, doses);
        RecordUse(player);
        RecordToleranceUse(player, product);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        if (player.Entity == null) return;
        ExpireHeroinEffect(player);
        Overdose.OnJoin(player, api.World.Calendar.TotalHours);
        // Older builds stacked screen effects up to 25 (morphine added 6 per dose). Bring saved
        // players back inside the vanilla range so the effect does not linger for many minutes.
        var watched = player.Entity.WatchedAttributes;
        if (watched.GetFloat("intoxication") > DrugVisualEffects.IntoxicationCap)
            watched.SetFloat("intoxication", DrugVisualEffects.IntoxicationCap);
        if (watched.GetFloat("psychedelic") > DrugVisualEffects.PsychedelicCap)
            watched.SetFloat("psychedelic", DrugVisualEffects.PsychedelicCap);
    }

    private void OnPlayerDeath(IServerPlayer player, DamageSource damageSource) => ClearAcuteEffects(player);
    private void OnPlayerRespawn(IServerPlayer player) => ClearAcuteEffects(player);

    private void ClearAcuteEffects(IPlayer player)
    {
        if (player.Entity == null) return;
        Overdose.Clear(player);
        foreach (string key in AcuteSpeedEffectKeys)
        {
            player.Entity.Stats.Remove("walkspeed", key);
            player.Entity.WatchedAttributes.RemoveAttribute(key + "-expires");
            player.Entity.WatchedAttributes.RemoveAttribute(key + "-expires-gamehour");
        }
    }

    public void Dispose()
    {
        api.Event.UnregisterGameTickListener(tickId);
        api.Event.PlayerJoin -= OnPlayerJoin;
        api.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
        api.Event.PlayerRespawn -= OnPlayerRespawn;
        api.Event.PlayerDeath -= OnPlayerDeath;
    }

    public static bool IsOverdosing(IPlayer player) => OverdoseSystem.IsOverdosing(player);

    private void OnPlayerJoin(IServerPlayer player)
    {
        var attrs = player.Entity.Attributes;
        if (!attrs.HasAttribute(AttrAddictionLevel)) attrs.SetInt(AttrAddictionLevel, 0);
        if (!attrs.HasAttribute(AttrDaysUsedConsecutively)) attrs.SetInt(AttrDaysUsedConsecutively, 0);
        SyncAddictionToClient(player);
    }

    private void RecordUse(IPlayer player)
    {
        var attrs = player.Entity.Attributes;
        int currentDay = (int)api.World.Calendar.TotalDays;
        int lastUseDay = attrs.GetInt(AttrLastUseDay);
        if (currentDay - lastUseDay <= 1) attrs.SetInt(AttrDaysUsedConsecutively, attrs.GetInt(AttrDaysUsedConsecutively) + 1);
        else attrs.SetInt(AttrDaysUsedConsecutively, 1);
        attrs.SetInt(AttrLastUseDay, currentDay);

        int consecutive = attrs.GetInt(AttrDaysUsedConsecutively);
        if (consecutive >= AddictionThreshold)
            attrs.SetInt(AttrAddictionLevel, Math.Min(attrs.GetInt(AttrAddictionLevel) + 1, 100));

        if (player is IServerPlayer serverPlayer) ClearWithdrawalEffects(serverPlayer);
        SyncAddictionToClient(player);
    }

    private void ProcessAllOnlinePlayers()
    {
        foreach (var player in api.Server.Players.Where(p => p.ConnectionState == EnumClientState.Playing))
        {
            ProcessWithdrawal(player);
            ProcessAddictionDecay(player);
            SyncAddictionToClient(player);
        }
    }

    private void ProcessWithdrawal(IServerPlayer player)
    {
        var attrs = player.Entity.Attributes;
        int addictionLevel = attrs.GetInt(AttrAddictionLevel);
        if (addictionLevel <= 0) return;
        int currentDay = (int)api.World.Calendar.TotalDays;
        int lastUseDay = attrs.GetInt(AttrLastUseDay);
        if (currentDay - lastUseDay >= 1)
        {
            ApplyWithdrawalEffects(player, WithdrawalSeverity(addictionLevel));
            if (!attrs.HasAttribute(AttrWithdrawalStartDay)) attrs.SetInt(AttrWithdrawalStartDay, currentDay);
        }
    }

    private void ProcessAddictionDecay(IServerPlayer player)
    {
        var attrs = player.Entity.Attributes;
        int addictionLevel = attrs.GetInt(AttrAddictionLevel);
        if (addictionLevel <= 0) return;
        int currentDay = (int)api.World.Calendar.TotalDays;
        int lastUseDay = attrs.GetInt(AttrLastUseDay);
        if (currentDay - lastUseDay >= DaysPerAddictionDecay)
        {
            attrs.SetInt(AttrAddictionLevel, addictionLevel - 1);
            if (addictionLevel - 1 <= 0)
            {
                attrs.RemoveAttribute(AttrDaysUsedConsecutively);
                attrs.RemoveAttribute(AttrWithdrawalStartDay);
                ClearWithdrawalEffects(player);
            }
        }
    }

    private void ApplyWithdrawalEffects(IServerPlayer player, float severity)
    {
        var entity = player.Entity;
        if (entity == null || !entity.Alive) return;
        entity.Stats.Set("walkspeed", WithdrawalEffectKey, -severity * WithdrawalSlowPerSeverity);
        if (severity > WithdrawalDamageSeverityGate)
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison }, severity * WithdrawalDamagePerSeverity);
    }

    private void ClearWithdrawalEffects(IServerPlayer player)
    {
        var entity = player.Entity;
        entity?.Stats.Remove("walkspeed", WithdrawalEffectKey);
    }

    private bool WithdrawalActive(IPlayer player)
    {
        var attrs = player.Entity.Attributes;
        if (attrs.GetInt(AttrAddictionLevel) <= 0) return false;
        return (int)api.World.Calendar.TotalDays - attrs.GetInt(AttrLastUseDay) >= 1;
    }

    private void SyncAddictionToClient(IPlayer player)
    {
        var entity = player.Entity;
        if (entity == null) return;
        var attrs = entity.Attributes;
        entity.WatchedAttributes.SetInt(WatchAddictionLevel, attrs.GetInt(AttrAddictionLevel));
        entity.WatchedAttributes.SetInt(WatchDaysUsed, attrs.GetInt(AttrDaysUsedConsecutively));
        entity.WatchedAttributes.SetBool(WatchWithdrawal, WithdrawalActive(player));
    }

    private static float WithdrawalSeverity(int addictionLevel) => WithdrawalSeverityBase + addictionLevel / 100f;
}
