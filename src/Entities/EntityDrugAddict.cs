using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsDope.Entities;

public class EntityDrugAddict : EntityAgent
{
    private enum AddictState { Approach, WaitInteract, Flee }

    // Tuning.
    // WalkVector is a *unit direction* scaled by a move speed, the same convention vanilla
    // AI tasks use (see Essentials' StraightLineTraverser). For scale, vanilla humanoids
    // walk at 0.035, a trader strolls at 0.01 and a wolf chases at 0.052 — so these are
    // roughly "jogging" and "sprinting" for a person.
    private const float WalkSpeed = 0.04f;          // closing on the player
    private const float FleeSpeed = 0.055f;         // running away
    private const float EngageDistance = 2.4f;      // stop-and-talk range
    private const float GiveUpDistance = 38;       // target too far -> leave
    private const float FleeDistance = 30;         // put this much ground between us and player, then vanish
    private const double InteractWindowSeconds = 45;   // patience while waiting to be dealt with
    private const double TradingWindowSeconds = 240;   // patience once the trade window is up
    private const double MaxLifetimeSeconds = 900;     // hard cap regardless of state
    private const double RetaliateSeconds = 6;         // how long we fight back when attacked
    private const double HostileChance = 0.35;         // chance a first engagement turns into a mugging

    private static readonly Random Rng = new();

    private AddictState state = AddictState.Approach;
    private string targetUid;
    private bool engagedOnce;
    private double ageSeconds;
    private double waitSeconds;
    private double retaliateUntil = -1;   // absolute ageSeconds deadline while fighting back

    public bool Friendly { get; private set; }
    public string TargetPlayerUid => targetUid;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        // IWorldAccessor.SpawnEntity() calls Initialize() *after* the spawn system has
        // already called BindTarget(), so only adopt the persisted uid when nothing is
        // bound yet. Reading unconditionally wipes a fresh binding and leaves the addict
        // targetless (it then flees on its very first tick and never approaches anyone).
        if (string.IsNullOrEmpty(targetUid)) targetUid = WatchedAttributes.GetString("vs-dope-addict-target");
    }

    // Called by the spawn system right before spawning to bind this addict to a player.
    public void BindTarget(string playerUid) => SetTarget(playerUid);

    private void SetTarget(string playerUid)
    {
        targetUid = playerUid;
        // Persist unconditionally so the binding survives Initialize(), chunk unload and relog.
        WatchedAttributes.SetString("vs-dope-addict-target", playerUid ?? "");
    }

    private EntityPlayer TargetEntity()
    {
        if (string.IsNullOrEmpty(targetUid)) return null;
        var player = World.PlayerByUid(targetUid) as IServerPlayer;
        return player?.Entity as EntityPlayer;
    }

    // EntityPlayer is the *entity*, not the player object, so `entityPlayer as IServerPlayer`
    // is always null. Resolve the real server player through the world's player registry.
    private IServerPlayer ServerPlayerOf(EntityPlayer entityPlayer)
        => entityPlayer == null ? null : World.PlayerByUid(entityPlayer.PlayerUID) as IServerPlayer;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);
        if (World.Side != EnumAppSide.Server || !Alive) return;

        ageSeconds += dt;

        if (ageSeconds > MaxLifetimeSeconds) { DespawnSelf(); return; }

        var target = TargetEntity();
        if (target == null || !target.Alive) { BeginFlee("leave"); return; }

        double distSq = HorizontalDistanceSq(target.Pos.X, target.Pos.Z);

        switch (state)
        {
            case AddictState.Approach:
                if (distSq > GiveUpDistance * GiveUpDistance) { DespawnSelf(); return; }
                if (distSq <= EngageDistance * EngageDistance)
                {
                    state = AddictState.WaitInteract;
                    waitSeconds = 0;
                    StopMoving();
                    Face(target.Pos.X, target.Pos.Z);
                    Say("vs-dope:addict-greet");
                }
                else
                {
                    MoveToward(target.Pos.X, target.Pos.Z, WalkSpeed);
                }
                break;

            case AddictState.WaitInteract:
                waitSeconds += dt;
                Face(target.Pos.X, target.Pos.Z);
                // Once the trade window is up the player needs time to dig through their
                // bags, so a dealing addict is far more patient than one still waiting to
                // be noticed. Walking off mid-trade is what used to strand the UI.
                if (waitSeconds > (Friendly ? TradingWindowSeconds : InteractWindowSeconds)) BeginFlee("impatient");
                // If the player wanders off mid-conversation, chase a little.
                else if (distSq > (EngageDistance * 2f) * (EngageDistance * 2f))
                    MoveToward(target.Pos.X, target.Pos.Z, WalkSpeed);
                else StopMoving();
                break;

            case AddictState.Flee:
                double away = distSq;
                if (away > FleeDistance * FleeDistance) { DespawnSelf(); return; }
                MoveAway(target.Pos.X, target.Pos.Z, FleeSpeed);
                break;
        }

        // Fighting back overrides waiting: chase and hit the attacker until deadline, then flee.
        if (ageSeconds < retaliateUntil && target != null)
        {
            if (distSq > EngageDistance * EngageDistance) MoveToward(target.Pos.X, target.Pos.Z, WalkSpeed);
            else AttackTarget(target);
        }
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
    {
        if (World.Side != EnumAppSide.Server || !Alive) return;
        if (mode == EnumInteractMode.Attack) { base.OnInteract(byEntity, slot, hitPosition, mode); return; }

        var player = byEntity as EntityPlayer;
        if (player == null) return;
        SetTarget(player.PlayerUID);

        var serverPlayer = ServerPlayerOf(player);
        // Diagnostic: this line appearing in server-debug.log proves the right-click actually
        // reached the server. Its absence means the client never sent the interaction, which
        // is usually a held item claiming the click before the entity ever sees it.
        World.Logger.VerboseDebug("[vs-dope] addict {0} interacted by {1} (resolved={2}, engaged={3}, friendly={4})",
            EntityId, player.PlayerUID, serverPlayer != null, engagedOnce, Friendly);
        if (serverPlayer == null) return;

        // First engagement decides the addict's fate: mug, or open the trading window.
        if (!engagedOnce)
        {
            engagedOnce = true;
            if (Rng.NextDouble() < HostileChance) { DoMug(byEntity); return; }

            Friendly = true;
            state = AddictState.WaitInteract;
            waitSeconds = 0;
            StopMoving();
            Face(player.Pos.X, player.Pos.Z);
            Say("vs-dope:addict-greet");
            VsDope.Systems.AddictTradeSystem.Instance?.OpenTradeFor(serverPlayer, this);
            return;
        }

        // Already engaged: a friendly addict just reopens its window.
        if (Friendly)
        {
            state = AddictState.WaitInteract;
            waitSeconds = 0;
            StopMoving();
            Face(player.Pos.X, player.Pos.Z);
            VsDope.Systems.AddictTradeSystem.Instance?.OpenTradeFor(serverPlayer, this);
        }
    }

    // Called by the trade system after a successful sale so the addict can react.
    // It deliberately stays put: bolting after the first sale used to despawn the addict
    // mid-session, which silently killed every later sale in the same window.
    public void OnPurchaseCompleted()
    {
        Say("vs-dope:addict-trade-thanks");
        state = AddictState.WaitInteract;
        waitSeconds = 0;
        StopMoving();
    }

    // The addict nods off on the high and dies on the spot.
    public void Overdose()
    {
        if (World.Side != EnumAppSide.Server || !Alive) return;
        Say("vs-dope:addict-overdose");
        Die(EnumDespawnReason.Death, new DamageSource { Source = EnumDamageSource.Unknown, Type = EnumDamageType.BluntAttack });
    }

    private void DoMug(EntityAgent victim)
    {
        var player = victim as EntityPlayer;
        var hand = player?.ActiveHandItemSlot;
        bool robbedSomething = false;
        if (hand?.Itemstack != null)
        {
            int qty = hand.Itemstack.StackSize;
            hand.TakeOut(qty);
            hand.MarkDirty();
            robbedSomething = true;
        }

        victim.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, 3f);

        Say(robbedSomething ? "vs-dope:addict-robbed" : "vs-dope:addict-mugged");
        BeginFlee("mugging");
    }

    private void AttackTarget(EntityPlayer target)
    {
        // Simple melee retaliation on a short cadence driven by the tick loop.
        if (ageSeconds - lastAttackAge < 1.2f) return;
        lastAttackAge = ageSeconds;
        target.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, 4f);
    }
    private double lastAttackAge;

    public override bool ReceiveDamage(DamageSource damageSource, float damage)
    {
        bool result = base.ReceiveDamage(damageSource, damage);
        if (World.Side == EnumAppSide.Server && Alive && damage > 0.5f && damageSource.SourceEntity?.EntityId != EntityId)
        {
            // Fight back for a while, then disengage and flee.
            retaliateUntil = ageSeconds + RetaliateSeconds;
            if (state == AddictState.WaitInteract) StopMoving();
        }
        return result;
    }

    private void BeginFlee(string reason)
    {
        if (state == AddictState.Flee) return;
        state = AddictState.Flee;
        StopMoving();
    }

    // ---- movement helpers -------------------------------------------------

    private void MoveToward(double tx, double tz, float speed)
    {
        double dx = tx - Pos.X;
        double dz = tz - Pos.Z;
        double len = Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-3) { StopMoving(); return; }
        Walk(dx / len, dz / len, speed);
        Face(tx, tz);
    }

    private void MoveAway(double tx, double tz, float speed)
    {
        double dx = Pos.X - tx;
        double dz = Pos.Z - tz;
        double len = Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-3) { StopMoving(); return; }
        Walk(dx / len, dz / len, speed);
        Face(tx, tz); // keep eyes on the threat while backing off
    }

    private void Walk(double dirX, double dirZ, float speed)
    {
        float scaled = speed * GlobalConstants.OverallSpeedMultiplier;
        ServerControls.WalkVector.Set(dirX * scaled, 0, dirZ * scaled);
        ServerControls.Forward = true;
        Controls.Forward = true;
    }

    private void StopMoving()
    {
        ServerControls.WalkVector.Set(0, 0, 0);
        ServerControls.Forward = false;
        Controls.Forward = false;
    }

    private void Face(double tx, double tz)
    {
        BodyYaw = (float)(Math.Atan2(tx - Pos.X, -(tz - Pos.Z)));
    }

    private double HorizontalDistanceSq(double tx, double tz)
    {
        double dx = tx - Pos.X;
        double dz = tz - Pos.Z;
        return dx * dx + dz * dz;
    }

    private void Say(string langKey)
    {
        if (World.Side != EnumAppSide.Server || string.IsNullOrEmpty(targetUid)) return;
        var player = World.PlayerByUid(targetUid);
        (player as IServerPlayer)?.SendLocalisedMessage(0, langKey, Array.Empty<object>());
    }

    private void DespawnSelf()
    {
        if (World.Side != EnumAppSide.Server) return;
        (World as IServerWorldAccessor)?.DespawnEntity(this, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
    }
}
