using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsDope.Systems;

public class AddictionSystem
{
    public const string AttrAddictionLevel = "vs-dope-addiction-level";
    private const string AttrLastUseDay = "vs-dope-last-use-day";
    public const string AttrDaysUsedConsecutively = "vs-dope-days-used";
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

    // Overdose: each product accumulates a per-product "load" (watched attribute) on use.
    // When load exceeds a tolerance-scaled threshold the player is overdosing: heavy slow +
    // escalating poison damage until metabolism brings load back down. Near-death but survivable.
    public const string WatchOverdose = "vs-dope-overdose";
    private const float OverdoseBaseThreshold = 15f;
    private const float TolerancePlateau = 0.5f;          // beyond this tolerance, threshold stops rising
    private const float ThresholdPerTolerancePoint = 20f; // +10 max bonus at the plateau
    private const float MetabolismPerTick = 1.5f;         // load units cleared per 5s tick while not overdosing-driven
    private const float OverdoseDamageAtMaxSeverity = 1.0f;
    private const float OverdoseDamageSeverityGate = 0.35f;
    private const string OverdoseEffectKey = "vs-dope-overdose";
    private const float HeroinDoseLoad = 5f;              // load added per detected heroin dose (not a DrugConsumableItem)
    private static readonly string[] OverdoseProducts = { "opium", "morphine", "heroin", "coca-vitae" };

    private static string LoadKey(string product) => $"vs-dope-load-{product}";

    // Tolerance is per finished product. The first two uses in an in-game day do not
    // increase tolerance. Heavy same-day use does, and days away from that product recover it.
    private const int FreeUsesPerDay = 2;
    private const float TolerancePerExcessUse = 0.08f;
    private const float MaxTolerance = 0.75f;
    private const float ToleranceRecoveryPerUnusedDay = 0.18f;
    private const float MinimumEffectMultiplier = 0.25f;

    // Overdose tracks a separate per-product concentration (raw, not tolerance-scaled).
    // It accumulates with each dose and metabolizes down over time on the tick timer.
    // Crossing the threshold slows hard and, past a gate, deals escalating poison damage.
    // Tolerance raises the threshold but only up to a plateau, so it can't be farmed forever.
    private const float OverdoseBaseThreshold = 15f;
    private const float TolerancePlateau = 0.5f;
    private const float ThresholdPerTolerancePoint = 20f;
    private const float MetabolismPerTick = 1.5f;
    private const float OverdoseDamageAtMaxSeverity = 1.0f;
    private const float OverdoseDamageSeverityGate = 0.35f;
    private const float HeroinDoseLoad = 5f;
    private const string OverdoseEffectKey = "vs-dope-overdose";
    public const string WatchOverdose = "vs-dope-overdose";
    private static readonly string[] OverdoseProducts = { "opium", "morphine", "heroin", "coca-vitae" };

    private ICoreServerAPI api;
    private long lastProcessedGameHour = -1;
    private readonly Dictionary<string, float> prevPsychedelicLevels = new();

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.PlayerLeave += player => prevPsychedelicLevels.Remove(player.PlayerUID);
        lastProcessedGameHour = -1;
        api.Event.Timer(OnTick, 5);
    }

    private static string TolKey(string product) => $"vs-dope-tolerance-{product}";
    private static string TolDayKey(string product) => $"vs-dope-tolerance-day-{product}";
    private static string TolUsesKey(string product) => $"vs-dope-tolerance-uses-{product}";
    public static string LoadKey(string product) => $"vs-dope-load-{product}";

    public float GetEffectMultiplier(IPlayer player, string product)
    {
        RecoverTolerance(player, product);
        float tolerance = Math.Clamp(player.Entity.Attributes.GetFloat(TolKey(product)), 0f, MaxTolerance);
        return Math.Max(MinimumEffectMultiplier, 1f - tolerance);
    }

    public void RecordToleranceUse(IPlayer player, string product)
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
            float tolerance = attrs.GetFloat(TolKey(product));
            float bingeScale = 1f + Math.Min(1.5f, (uses - FreeUsesPerDay - 1) * 0.15f);
            attrs.SetFloat(TolKey(product), Math.Min(MaxTolerance, tolerance + TolerancePerExcessUse * bingeScale));
        }
    }

    private void RecoverTolerance(IPlayer player, string product)
    {
        var attrs = player.Entity.Attributes;
        int today = (int)api.World.Calendar.TotalDays;
        int lastDay = attrs.GetInt(TolDayKey(product));
        if (lastDay <= 0 || lastDay >= today) return;

        int unusedDays = today - lastDay;
        float tolerance = Math.Max(0f, attrs.GetFloat(TolKey(product)) - unusedDays * ToleranceRecoveryPerUnusedDay);
        attrs.SetFloat(TolKey(product), tolerance);
        attrs.SetInt(TolDayKey(product), today);
        attrs.SetInt(TolUsesKey(product), 0);
    }

    private void OnTick()
    {
        if (api.World?.Calendar == null) return;
        WatchHeroinEffects();
        MetabolizeAndCheckOverdose();

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

    private void WatchHeroinEffects()
    {
        foreach (var player in api.Server.Players.Where(p => p.ConnectionState == EnumClientState.Playing))
        {
            var entity = player.Entity;
            if (entity == null || !entity.Alive) continue;

            double heroinSpeedExpires = entity.WatchedAttributes.GetDouble(HeroinSpeedExpiryKey);
            if (heroinSpeedExpires > 0 && api.World.Calendar.TotalHours >= heroinSpeedExpires)
            {
                entity.Stats.Remove("walkspeed", HeroinSpeedEffectKey);
                entity.WatchedAttributes.RemoveAttribute(HeroinSpeedExpiryKey);
            }

            float currentPsych = entity.WatchedAttributes.GetFloat("psychedelic");
            string uid = player.PlayerUID;
            prevPsychedelicLevels.TryGetValue(uid, out float prevPsych);

            if (currentPsych > prevPsych && currentPsych > 0.1f)
            {
                float rawIncrease = currentPsych - prevPsych;
                float effectMultiplier = GetEffectMultiplier(player, "heroin");
                if (effectMultiplier < 1f)
                {
                    currentPsych = prevPsych + rawIncrease * effectMultiplier;
                    entity.WatchedAttributes.SetFloat("psychedelic", currentPsych);
                }

                RecordUse(player);
                RecordToleranceUse(player, "heroin");
                entity.WatchedAttributes.SetFloat(LoadKey("heroin"), entity.WatchedAttributes.GetFloat(LoadKey("heroin")) + HeroinDoseLoad);
                float effectiveSlow = HeroinSlowFactor * effectMultiplier;
                entity.Stats.Set("walkspeed", HeroinSpeedEffectKey, -effectiveSlow);
                entity.WatchedAttributes.SetDouble(
                    HeroinSpeedExpiryKey,
                    api.World.Calendar.TotalHours + HeroinSpeedDurationGameHours
                );
            }

            prevPsychedelicLevels[uid] = currentPsych;
        }
    }

    private void MetabolizeAndCheckOverdose()
    {
        foreach (var player in api.Server.Players.Where(p => p.ConnectionState == EnumClientState.Playing))
        {
            var entity = player.Entity;
            if (entity == null) continue;

            bool anyOverdose = false;
            foreach (string product in OverdoseProducts)
            {
                string key = LoadKey(product);
                float load = entity.WatchedAttributes.GetFloat(key);
                if (load <= 0f) continue;

                load = Math.Max(0f, load - MetabolismPerTick);
                if (load <= 0f) entity.WatchedAttributes.RemoveAttribute(key);
                else entity.WatchedAttributes.SetFloat(key, load);

                float tolerance = Math.Clamp(entity.Attributes.GetFloat(TolKey(product)), 0f, MaxTolerance);
                float threshold = OverdoseBaseThreshold + Math.Min(tolerance, TolerancePlateau) * ThresholdPerTolerancePoint;
                if (load <= threshold) continue;

                anyOverdose = true;
                if (!entity.Alive) continue;

                float severity = Math.Clamp((load - threshold) / threshold, 0f, 1f);
                entity.Stats.Set("walkspeed", OverdoseEffectKey, -Math.Min(0.85f, severity));
                if (severity > OverdoseDamageSeverityGate)
                    entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison }, severity * OverdoseDamageAtMaxSeverity);
            }

            if (!anyOverdose && entity.WatchedAttributes.GetBool(WatchOverdose))
                entity.Stats.Remove("walkspeed", OverdoseEffectKey);

            entity.WatchedAttributes.SetBool(WatchOverdose, anyOverdose);
        }
    }

    public static bool IsOverdosing(IPlayer player) => player.Entity?.WatchedAttributes.GetBool(WatchOverdose) ?? false;

    private void OnPlayerJoin(IServerPlayer player)
    {
        var attrs = player.Entity.Attributes;
        if (!attrs.HasAttribute(AttrAddictionLevel)) attrs.SetInt(AttrAddictionLevel, 0);
        if (!attrs.HasAttribute(AttrDaysUsedConsecutively)) attrs.SetInt(AttrDaysUsedConsecutively, 0);
        SyncAddictionToClient(player);
    }

    public void RecordUse(IPlayer player)
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
            float severity = WithdrawalSeverityBase + addictionLevel / 100f;
            ApplyWithdrawalEffects(player, severity);
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
        entity.Stats.Set("walkspeed", "vs-dope-withdrawal", -severity * 0.4f);
        if (severity > 0.8f)
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison }, severity * 1.5f);
    }

    private void ClearWithdrawalEffects(IServerPlayer player)
    {
        var entity = player.Entity;
        if (entity != null) entity.Stats.Remove("walkspeed", "vs-dope-withdrawal");
    }

    public bool WithdrawalActive(IPlayer player)
    {
        var attrs = player.Entity.Attributes;
        if (attrs.GetInt(AttrAddictionLevel) <= 0) return false;
        return (int)api.World.Calendar.TotalDays - attrs.GetInt(AttrLastUseDay) >= 1;
    }

    public void SyncAddictionToClient(IPlayer player)
    {
        var entity = player.Entity;
        if (entity == null) return;
        var attrs = entity.Attributes;
        entity.WatchedAttributes.SetInt(WatchAddictionLevel, attrs.GetInt(AttrAddictionLevel));
        entity.WatchedAttributes.SetInt(WatchDaysUsed, attrs.GetInt(AttrDaysUsedConsecutively));
        entity.WatchedAttributes.SetBool(WatchWithdrawal, WithdrawalActive(player));
    }

    public static int GetAddictionLevel(IPlayer player) => player.Entity.Attributes.GetInt(AttrAddictionLevel);
    public static bool IsAddicted(IPlayer player) => player.Entity.Attributes.GetInt(AttrAddictionLevel) > 0;
    public static float GetWithdrawalSeverity(IPlayer player)
    {
        int level = player.Entity.Attributes.GetInt(AttrAddictionLevel);
        return level <= 0 ? 0f : WithdrawalSeverityBase + level / 100f;
    }
}
