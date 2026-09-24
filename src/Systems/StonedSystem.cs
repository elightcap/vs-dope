using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsDope.Systems;

/// <summary>Calendar-based marijuana effect. Reapplication refreshes one timer, never stacks.</summary>
public sealed class StonedSystem : ModSystem
{
    public const string EffectKey = "vs-dope-stoned";
    public const string ExpiryKey = EffectKey + "-expires-gamehour";
    private const string LastHourKey = EffectKey + "-last-gamehour";
    public const double DurationHours = 2;
    public const float SpeedModifier = -0.2f;
    public const float HealPerGameMinute = 0.5f;
    private ICoreServerAPI? sapi;
    private long tickId;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Event.PlayerNowPlaying += OnPlaying;
        api.Event.PlayerRespawn += OnRespawn;
        api.Event.PlayerDeath += OnDeath;
        api.Event.PlayerDisconnect += OnDisconnect;
        tickId = api.Event.RegisterGameTickListener(OnTick, 250);
    }

    public static bool IsActive(EntityAgent entity, double now) => entity.Alive &&
        entity.WatchedAttributes.GetDouble(ExpiryKey) > now;

    public static void Apply(EntityAgent entity, double now)
    {
        if (entity.World.Side != EnumAppSide.Server || !entity.Alive) return;
        Tick(entity, now); // Settle the old interval before refreshing it.
        entity.WatchedAttributes.SetDouble(ExpiryKey, now + DurationHours);
        entity.Attributes.SetDouble(LastHourKey, now);
        entity.Stats.Set("walkspeed", EffectKey, SpeedModifier, true);
    }

    public static void Tick(EntityAgent entity, double now)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        double expiry = entity.WatchedAttributes.GetDouble(ExpiryKey);
        if (expiry <= 0) return;
        if (!entity.Alive) { Clear(entity); return; }
        double last = entity.Attributes.GetDouble(LastHourKey, now);
        double through = Math.Min(now, expiry);
        // Fractional minutes count, including the final interval at expiry. No wall-clock healing.
        double elapsed = Math.Max(0, through - last);
        if (elapsed > 0)
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal },
                (float)(elapsed * 60 * HealPerGameMinute));
        entity.Attributes.SetDouble(LastHourKey, now);
        if (now >= expiry) Clear(entity);
    }

    public static void Resume(EntityAgent entity, double now)
    {
        // World time can pass while disconnected; expiry persists but offline healing is not banked.
        entity.Attributes.SetDouble(LastHourKey, now);
        if (!IsActive(entity, now)) Clear(entity);
        else entity.Stats.Set("walkspeed", EffectKey, SpeedModifier, true);
    }

    public static void Clear(EntityAgent entity)
    {
        entity.Stats.Remove("walkspeed", EffectKey);
        if (entity.WatchedAttributes.HasAttribute(ExpiryKey)) entity.WatchedAttributes.RemoveAttribute(ExpiryKey);
        entity.Attributes.RemoveAttribute(LastHourKey);
    }

    private void OnTick(float dt)
    {
        foreach (var player in sapi!.World.AllOnlinePlayers)
            if (player is IServerPlayer serverPlayer && serverPlayer.ConnectionState == EnumClientState.Playing && player.Entity != null)
                Tick(player.Entity, sapi.World.Calendar.TotalHours);
    }
    private void OnPlaying(IServerPlayer player) => Resume(player.Entity, sapi!.World.Calendar.TotalHours);
    private void OnRespawn(IServerPlayer player) => Clear(player.Entity);
    private void OnDeath(IServerPlayer player, DamageSource source) => Clear(player.Entity);
    private void OnDisconnect(IServerPlayer player) => Tick(player.Entity, sapi!.World.Calendar.TotalHours);

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.UnregisterGameTickListener(tickId);
            sapi.Event.PlayerNowPlaying -= OnPlaying;
            sapi.Event.PlayerRespawn -= OnRespawn;
            sapi.Event.PlayerDeath -= OnDeath;
            sapi.Event.PlayerDisconnect -= OnDisconnect;
        }
        base.Dispose();
    }
}
