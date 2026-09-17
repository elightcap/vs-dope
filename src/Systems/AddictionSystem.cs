using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsDope.Systems;

public class AddictionSystem
{
    // Server-side persistence (survives relog)
    public const string AttrAddictionLevel = "vs-dope-addiction-level";
    private const string AttrLastUseDay = "vs-dope-last-use-day";
    public const string AttrDaysUsedConsecutively = "vs-dope-days-used";
    private const string AttrWithdrawalStartDay = "vs-dope-withdrawal-start";

    // Client-synced copies (read by the character screen tab)
    public const string WatchAddictionLevel = "vs-dope-addiction-level";
    public const string WatchDaysUsed = "vs-dope-days-used";
    public const string WatchWithdrawal = "vs-dope-withdrawal";

    private const int AddictionThreshold = 5;
    private const float WithdrawalSeverityBase = 0.3f;
    private const int DaysPerAddictionDecay = 2;

    private ICoreServerAPI api;
    private double lastProcessedHour;

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        api.Event.PlayerJoin += OnPlayerJoin;
        lastProcessedHour = -1;
        api.Event.Timer(OnTick, 10);
    }

    private void OnTick()
    {
        if (api.World?.Calendar == null) return;
        double currentHour = api.World.Calendar.ElapsedHours;
        if ((int)currentHour != (int)lastProcessedHour)
        {
            lastProcessedHour = currentHour;
            int hourOfDay = api.World.Calendar.FullHourOfDay;
            if (hourOfDay == 0)
            {
                ProcessAllOnlinePlayers();
            }
        }
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        var attrs = player.Entity.Attributes;
        if (!attrs.HasAttribute(AttrAddictionLevel))
            attrs.SetInt(AttrAddictionLevel, 0);
        if (!attrs.HasAttribute(AttrDaysUsedConsecutively))
            attrs.SetInt(AttrDaysUsedConsecutively, 0);

        SyncAddictionToClient(player);
    }

    public void RecordUse(IPlayer player)
    {
        var attrs = player.Entity.Attributes;
        int currentDay = (int)api.World.Calendar.TotalDays;
        int lastUseDay = attrs.GetInt(AttrLastUseDay);

        if (currentDay - lastUseDay <= 1)
        {
            attrs.SetInt(AttrDaysUsedConsecutively, attrs.GetInt(AttrDaysUsedConsecutively) + 1);
        }
        else
        {
            attrs.SetInt(AttrDaysUsedConsecutively, 1);
        }

        attrs.SetInt(AttrLastUseDay, currentDay);

        int consecutive = attrs.GetInt(AttrDaysUsedConsecutively);
        if (consecutive >= AddictionThreshold)
        {
            int level = System.Math.Min(attrs.GetInt(AttrAddictionLevel) + 1, 100);
            attrs.SetInt(AttrAddictionLevel, level);
        }

        var serverPlayer = player as IServerPlayer;
        if (serverPlayer != null)
            ClearWithdrawalEffects(serverPlayer);

        SyncAddictionToClient(player);
    }

    private void ProcessAllOnlinePlayers()
    {
        var onlinePlayers = api.Server.Players
            .Where(p => p.ConnectionState == EnumClientState.Playing);

        foreach (var player in onlinePlayers)
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
            float severity = WithdrawalSeverityBase + (addictionLevel / 100f);
            ApplyWithdrawalEffects(player, severity);

            if (!attrs.HasAttribute(AttrWithdrawalStartDay))
                attrs.SetInt(AttrWithdrawalStartDay, currentDay);
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

        float slowdownMultiplier = 1f - (severity * 0.4f);
        entity.Stats.Set("walkspeed", "vs-dope-withdrawal", slowdownMultiplier);

        if (severity > 0.8f)
        {
            entity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Poison
            }, severity * 1.5f);
        }
    }

    private void ClearWithdrawalEffects(IServerPlayer player)
    {
        var entity = player.Entity;
        if (entity == null) return;

        entity.Stats.Remove("walkspeed", "vs-dope-withdrawal");
    }

    public bool WithdrawalActive(IPlayer player)
    {
        var attrs = player.Entity.Attributes;
        if (attrs.GetInt(AttrAddictionLevel) <= 0) return false;
        int currentDay = (int)api.World.Calendar.TotalDays;
        int lastUseDay = attrs.GetInt(AttrLastUseDay);
        return currentDay - lastUseDay >= 1;
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

    public static int GetAddictionLevel(IPlayer player) =>
        player.Entity.Attributes.GetInt(AttrAddictionLevel);

    public static bool IsAddicted(IPlayer player) =>
        player.Entity.Attributes.GetInt(AttrAddictionLevel) > 0;

    public static float GetWithdrawalSeverity(IPlayer player)
    {
        int level = player.Entity.Attributes.GetInt(AttrAddictionLevel);
        return level <= 0 ? 0f : WithdrawalSeverityBase + (level / 100f);
    }
}
