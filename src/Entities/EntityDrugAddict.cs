using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VsDope.Systems;

namespace VsDope.Entities;

public class EntityDrugAddict : EntityAgent
{
    // Leave: the addict is done with the player (sold to, or its target is gone). It lingers
    // briefly after a sale so the player can keep selling, then walks off and despawns once
    // no player can see it. Distinct from Flee, which is the hostile/impatient bolt.
    private enum AddictState { Approach, WaitInteract, Flee, Leave }

    // Tuning.
    // WalkVector is a *unit direction* scaled by a move speed w, the same convention vanilla AI
    // tasks use (Essentials' WaypointsTraverser). Real ground speed, from the 1.22.7 physics
    // (PModuleOnGround: per 1/60 s sub-step motion += w, motion *= 0.7; PModuleMotionDrag 0.983;
    // server physics ticks at 1/30 s and moves motion * dt * 60):
    //     server entity speed ~= 135 * w blocks/s
    // A player's WalkVector is dt * BaseMoveSpeed(1.5) = 0.025 at the client's 1/60 s tick, so
    //     player walk ~= 3.4 b/s (w-equivalent 0.025), sprint (x2) ~= 6.8 b/s (w-equivalent 0.05).
    // The old 0.04 / 0.055 / 0.03 were 5.4 / 7.4 / 4.0 b/s: fleeing addicts outran a sprinting
    // player (issue #38). Block walk-speed multipliers apply to both equally. See
    // .planning/2026-09-24-addict-inventory/findings.md for the derivation.
    private const float WalkSpeed = 0.025f;         // closing on the player: ~3.4 b/s, about player walk
    private const float FleeSpeed = 0.034f;         // running away: ~4.6 b/s net, beats a walker, a sprinter catches it
    // Fleeing sets Controls.Sprint so the shape plays its run animation. Sprint also multiplies
    // ground speed by GlobalConstants.SprintSpeedMultiplier (2.0) in EntityAgent.GetWalkSpeedMultiplier
    // for *any* EntityAgent, so Walk() divides it back out: the speeds above are always net speeds.
    private const float LeaveSpeed = 0.018f;        // strolling off after a deal: ~2.4 b/s
    private const float EngageDistance = 2.4f;      // stop-and-talk range
    private const float GiveUpDistance = 38;       // target too far -> leave
    private const float FleeDistance = 30;         // put this much ground between us and player, then vanish
    private const double InteractWindowSeconds = 45;   // patience while waiting to be dealt with
    private const double TradingWindowSeconds = 240;   // patience once the trade window is up
    private const double MaxLifetimeSeconds = 900;     // hard cap regardless of state
    private const double RetaliateSeconds = 6;         // how long we fight back when attacked
    private const double HostileChance = 0.35;         // chance a first engagement turns into a mugging
    private const float MugDamage = 3f;
    private const float RetaliateDamage = 4f;
    private const double AttackCooldownSeconds = 1.2;

    // Leaving.
    private const double LingerAfterSaleSeconds = 10;  // grace to sell more from the open window; reset by each sale
    private const double LeaveMinWalkSeconds = 4;      // always visibly walk off before vanishing
    private const double LeaveTimeoutGameHours = 1;    // fallback despawn (~2 real minutes at default day length)
    private const double LeaveHeadingJitterRad = 0.6;  // +/- ~35 degrees around "straight away from the player"
    private const float LeaveAlwaysVisibleDistance = 24; // closer than this counts as seen, whatever the view
    private const float LeaveVisibleDistance = 64;       // farther than this counts as out of sight
    private const double LeaveViewConeCos = 0.5;         // within ~60 degrees of a player's look direction = seen
    private const double StuckCheckSeconds = 1.5;        // how often to test whether the walk made progress
    private const double StuckMinProgress = 0.4;         // blocks per check; less than this = stuck, turn

    // Persisted state (WatchedAttributes, saved with the entity so reloads don't reset it).
    private const string AttrTarget = "vs-dope-addict-target";
    private const string AttrState = "vs-dope-addict-state";
    private const string AttrEngaged = "vs-dope-addict-engaged";
    private const string AttrFriendly = "vs-dope-addict-friendly";
    private const string AttrLeaveHeading = "vs-dope-addict-leave-heading";
    private const string AttrLeaveStartHours = "vs-dope-addict-leave-hours";
    // Server-only (Entity.Attributes, saved with the entity, never synced): the addict's pockets.
    private const string AttrPockets = "vs-dope-addict-inv";

    private static readonly Random Rng = new();

    private AddictState state = AddictState.Approach;
    private string? targetUid;
    private bool engagedOnce;
    private double ageSeconds;
    private double waitSeconds;
    private double retaliateUntil = -1;   // absolute ageSeconds deadline while fighting back
    private double lastAttackAge;

    private double lingerUntil = -1;      // absolute ageSeconds; stand around after a sale until then
    private bool leaveWalking;
    private double leaveHeading;          // radians, atan2(dz, dx)
    private double leaveWalkSeconds;
    private double stuckCheckTimer;
    private double stuckCheckX, stuckCheckZ;

    private AddictPockets? pockets;       // server only; loaded lazily, see Pockets

    public bool Friendly { get; private set; }
    public string? TargetPlayerUid => targetUid;

    // Trades are accepted while waiting on the player, and during the short linger after a sale.
    public bool AcceptsTrade =>
        Alive && Friendly &&
        (state == AddictState.WaitInteract || (state == AddictState.Leave && !leaveWalking && ageSeconds < lingerUntil));

    /// <summary>
    /// The addict's inventory (server only, null on the client). Loaded on first access rather
    /// than in Initialize so it always runs after FromBytes has restored Attributes. A fresh
    /// addict has no saved pockets and rolls its starting stock here.
    /// </summary>
    public AddictPockets? Pockets
    {
        get
        {
            if (pockets != null || World == null || World.Side != EnumAppSide.Server) return pockets;
            pockets = new AddictPockets();
            if (Attributes.GetTreeAttribute(AttrPockets) is { } saved) pockets.Load(saved, World);
            else
            {
                pockets.RollStartingStock(World, Rng);
                SavePockets();
            }
            return pockets;
        }
    }

    /// <summary>Write the pockets back into the persisted attributes. Call after every change.</summary>
    public void SavePockets()
    {
        if (pockets != null) Attributes[AttrPockets] = pockets.ToTree();
    }

    // Entity.Die calls this only for EnumDespawnReason.Death (player kill or Overdose()), so a
    // despawn never drops anything. The pockets are emptied so nothing can drop twice.
    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
    {
        var drops = new List<ItemStack>();
        var baseDrops = base.GetDrops(world, pos, byPlayer);
        if (baseDrops != null) drops.AddRange(baseDrops);
        if (world.Side == EnumAppSide.Server && Pockets is { } p)
        {
            drops.AddRange(p.TakeAll());
            SavePockets();
        }
        return drops.ToArray();
    }

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        // IWorldAccessor.SpawnEntity() calls Initialize() *after* the spawn system has
        // already called BindTarget(), so only adopt the persisted uid when nothing is
        // bound yet. Reading unconditionally wipes a fresh binding and leaves the addict
        // targetless (it then flees on its very first tick and never approaches anyone).
        if (string.IsNullOrEmpty(targetUid)) targetUid = WatchedAttributes.GetString(AttrTarget);

        // Restore the lifecycle after a chunk reload / server restart. A fresh spawn has none
        // of these keys and keeps its defaults.
        if (WatchedAttributes.HasAttribute(AttrState) &&
            Enum.TryParse(WatchedAttributes.GetString(AttrState), out AddictState saved))
        {
            state = saved;
        }
        engagedOnce = WatchedAttributes.GetBool(AttrEngaged, engagedOnce);
        Friendly = WatchedAttributes.GetBool(AttrFriendly, Friendly);
        if (state == AddictState.Leave && WatchedAttributes.HasAttribute(AttrLeaveHeading))
        {
            // Already walking away when saved: carry on in the same direction, no second linger.
            leaveWalking = true;
            leaveHeading = WatchedAttributes.GetDouble(AttrLeaveHeading);
        }
    }

    // Called by the spawn system right before spawning to bind this addict to a player.
    public void BindTarget(string playerUid) => SetTarget(playerUid);

    private void SetTarget(string? playerUid)
    {
        targetUid = playerUid;
        // Persist unconditionally so the binding survives Initialize(), chunk unload and relog.
        WatchedAttributes.SetString(AttrTarget, playerUid ?? "");
    }

    private void SetState(AddictState newState)
    {
        state = newState;
        WatchedAttributes.SetString(AttrState, newState.ToString());
    }

    private void SetEngaged(bool friendly)
    {
        engagedOnce = true;
        Friendly = friendly;
        WatchedAttributes.SetBool(AttrEngaged, true);
        WatchedAttributes.SetBool(AttrFriendly, friendly);
    }

    private EntityPlayer? TargetEntity()
    {
        if (string.IsNullOrEmpty(targetUid)) return null;
        var player = World.PlayerByUid(targetUid) as IServerPlayer;
        return player?.Entity as EntityPlayer;
    }

    // EntityPlayer is the *entity*, not the player object, so `entityPlayer as IServerPlayer`
    // is always null. Resolve the real server player through the world's player registry.
    private IServerPlayer? ServerPlayerOf(EntityPlayer entityPlayer)
        => World.PlayerByUid(entityPlayer.PlayerUID) as IServerPlayer;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);
        if (World.Side != EnumAppSide.Server || !Alive) return;

        ageSeconds += dt;
        _ = Pockets; // roll/load the inventory early so it exists before anyone trades or kills us

        if (ageSeconds > MaxLifetimeSeconds) { DespawnSelf(); return; }

        var target = TargetEntity();
        if (target != null && !target.Alive) target = null;

        if (state == AddictState.Leave)
        {
            if (TickLeave(target, dt)) return;
            TickRetaliation(target);
            return;
        }

        // Target logged off or died: nobody to deal with, so wander off like after a sale.
        if (target == null) { BeginLeave(0); return; }

        double distSq = HorizontalDistanceSq(target.Pos.X, target.Pos.Z);

        switch (state)
        {
            case AddictState.Approach:
                if (distSq > GiveUpDistance * GiveUpDistance) { DespawnSelf(); return; }
                if (distSq <= EngageDistance * EngageDistance)
                {
                    SetState(AddictState.WaitInteract);
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
                if (waitSeconds > (Friendly ? TradingWindowSeconds : InteractWindowSeconds)) BeginFlee();
                // If the player wanders off mid-conversation, chase a little.
                else if (distSq > (EngageDistance * 2f) * (EngageDistance * 2f))
                    MoveToward(target.Pos.X, target.Pos.Z, WalkSpeed);
                else StopMoving();
                break;

            case AddictState.Flee:
                if (distSq > FleeDistance * FleeDistance) { DespawnSelf(); return; }
                MoveAway(target.Pos.X, target.Pos.Z, FleeSpeed, sprint: true);
                break;
        }

        TickRetaliation(target);
    }

    // Fighting back overrides everything else: chase and hit the attacker until the deadline.
    private void TickRetaliation(EntityPlayer? target)
    {
        if (target == null || ageSeconds >= retaliateUntil) return;
        double distSq = HorizontalDistanceSq(target.Pos.X, target.Pos.Z);
        if (distSq > EngageDistance * EngageDistance) MoveToward(target.Pos.X, target.Pos.Z, WalkSpeed);
        else AttackTarget(target);
    }

    // Returns true when the addict despawned this tick.
    private bool TickLeave(EntityPlayer? target, float dt)
    {
        // Post-sale linger: stay put and face the player so the open window keeps working.
        if (!leaveWalking && ageSeconds < lingerUntil && target != null)
        {
            StopMoving();
            Face(target.Pos.X, target.Pos.Z);
            return false;
        }

        if (!leaveWalking) StartLeaveWalk(target);

        leaveWalkSeconds += dt;
        bool timedOut = World.Calendar.TotalHours - WatchedAttributes.GetDouble(AttrLeaveStartHours, World.Calendar.TotalHours) > LeaveTimeoutGameHours;
        if (timedOut || (leaveWalkSeconds > LeaveMinWalkSeconds && !AnyPlayerCanSee()))
        {
            DespawnSelf();
            return true;
        }

        // Crude obstacle handling for manual steering: hop when blocked, and turn if the
        // last few seconds made no real progress (wall, cliff, water edge).
        stuckCheckTimer += dt;
        if (stuckCheckTimer >= StuckCheckSeconds)
        {
            double mx = Pos.X - stuckCheckX, mz = Pos.Z - stuckCheckZ;
            if (mx * mx + mz * mz < StuckMinProgress * StuckMinProgress)
            {
                SetLeaveHeading(leaveHeading + (Rng.NextDouble() < 0.5 ? -1 : 1) * Math.PI / 2);
            }
            stuckCheckTimer = 0;
            stuckCheckX = Pos.X;
            stuckCheckZ = Pos.Z;
        }

        double dirX = Math.Cos(leaveHeading), dirZ = Math.Sin(leaveHeading);
        Walk(dirX, dirZ, LeaveSpeed);
        Face(Pos.X + dirX, Pos.Z + dirZ);
        bool jump = CollidedHorizontally && OnGround;
        ServerControls.Jump = jump;
        Controls.Jump = jump;
        return false;
    }

    private void StartLeaveWalk(EntityPlayer? target)
    {
        double heading;
        if (target != null)
        {
            double dx = Pos.X - target.Pos.X, dz = Pos.Z - target.Pos.Z;
            heading = (dx * dx + dz * dz) > 1e-4 ? Math.Atan2(dz, dx) : Rng.NextDouble() * Math.PI * 2;
            heading += (Rng.NextDouble() * 2 - 1) * LeaveHeadingJitterRad;
        }
        else heading = Rng.NextDouble() * Math.PI * 2;

        leaveWalking = true;
        leaveWalkSeconds = 0;
        stuckCheckTimer = 0;
        stuckCheckX = Pos.X;
        stuckCheckZ = Pos.Z;
        SetLeaveHeading(heading);
        if (!WatchedAttributes.HasAttribute(AttrLeaveStartHours))
            WatchedAttributes.SetDouble(AttrLeaveStartHours, World.Calendar.TotalHours);

        // The deal is over: take the trade window away instead of leaving it dangling.
        AddictTradeSystem.Instance?.CloseTradeFor(this);
        if (Friendly && target != null) Say("vs-dope:addict-leaving");
    }

    private void SetLeaveHeading(double heading)
    {
        leaveHeading = heading;
        WatchedAttributes.SetDouble(AttrLeaveHeading, heading);
    }

    // "Out of render" approximation: nobody close, and nobody within view range looking our way.
    private bool AnyPlayerCanSee()
    {
        foreach (var player in World.AllOnlinePlayers)
        {
            if (player is IServerPlayer sp && sp.ConnectionState != EnumClientState.Playing) continue;
            var eye = player.Entity;
            if (eye == null) continue;

            double dx = Pos.X - eye.Pos.X, dy = Pos.Y - eye.Pos.Y, dz = Pos.Z - eye.Pos.Z;
            double distSq = dx * dx + dy * dy + dz * dz;
            if (distSq < LeaveAlwaysVisibleDistance * LeaveAlwaysVisibleDistance) return true;
            if (distSq > LeaveVisibleDistance * LeaveVisibleDistance) continue;

            Vec3f view = eye.Pos.GetViewVector();
            double viewLen = Math.Sqrt(view.X * view.X + view.Z * view.Z);
            double hLen = Math.Sqrt(dx * dx + dz * dz);
            if (viewLen < 1e-3 || hLen < 1e-3) return true; // looking straight up/down: be conservative
            if ((view.X * dx + view.Z * dz) / (viewLen * hLen) > LeaveViewConeCos) return true;
        }
        return false;
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
    {
        // Attacks must run the vanilla path on *both* sides. The client half plays the hit
        // sound, hurt animation and damage feedback locally; skipping it made hits look like
        // they did nothing at all.
        if (mode == EnumInteractMode.Attack) { base.OnInteract(byEntity, slot, hitPosition, mode); return; }
        if (World.Side != EnumAppSide.Server || !Alive) return;

        if (byEntity is not EntityPlayer player) return;

        var serverPlayer = ServerPlayerOf(player);
        // Diagnostic: this line appearing in server-debug.log proves the right-click actually
        // reached the server. Its absence means the client never sent the interaction, which
        // is usually a held item claiming the click before the entity ever sees it.
        World.Logger.VerboseDebug("[vs-dope] addict {0} interacted by {1} (resolved={2}, engaged={3}, friendly={4}, state={5})",
            EntityId, player.PlayerUID, serverPlayer != null, engagedOnce, Friendly, state);
        if (serverPlayer == null) return;

        // Walking away (or bolting): the addict is done and won't be pulled back in.
        if (state == AddictState.Flee || (state == AddictState.Leave && !AcceptsTrade))
        {
            serverPlayer.SendLocalisedMessage(0, "vs-dope:addict-busy-leaving", Array.Empty<object>());
            return;
        }

        SetTarget(player.PlayerUID);

        // First engagement decides the addict's fate: mug, or open the trading window.
        if (!engagedOnce)
        {
            if (Rng.NextDouble() < HostileChance) { SetEngaged(false); DoMug(byEntity); return; }

            SetEngaged(true);
            SetState(AddictState.WaitInteract);
            waitSeconds = 0;
            StopMoving();
            Face(player.Pos.X, player.Pos.Z);
            Say("vs-dope:addict-greet");
            AddictTradeSystem.Instance?.OpenTradeFor(serverPlayer, this);
            return;
        }

        // Already engaged: a friendly addict just reopens its window (a lingering one keeps lingering).
        if (Friendly)
        {
            if (state != AddictState.Leave) SetState(AddictState.WaitInteract);
            waitSeconds = 0;
            StopMoving();
            Face(player.Pos.X, player.Pos.Z);
            AddictTradeSystem.Instance?.OpenTradeFor(serverPlayer, this);
        }
    }

    // Called by the trade system after a successful sale. The addict hangs around for a short
    // grace period (so the player can keep selling from the same window; every sale resets it),
    // then walks away and despawns out of sight.
    public void OnPurchaseCompleted()
    {
        Say("vs-dope:addict-trade-thanks");
        BeginLeave(LingerAfterSaleSeconds);
    }

    private void BeginLeave(double lingerSeconds)
    {
        if (state == AddictState.Leave && leaveWalking) return;
        SetState(AddictState.Leave);
        lingerUntil = ageSeconds + lingerSeconds;
        waitSeconds = 0;
        StopMoving();
    }

    // The addict nods off on the high and dies on the spot.
    public void Overdose()
    {
        if (World.Side != EnumAppSide.Server || !Alive) return;
        Say("vs-dope:addict-overdose");
        Die(EnumDespawnReason.Death, new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison });
    }

    public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource? damageSourceForDeath = null)
    {
        bool wasAlive = Alive;
        base.Die(reason, damageSourceForDeath);
        // Dead addicts don't trade. The corpse itself is handled by the deaddecay behavior.
        if (wasAlive && World.Side == EnumAppSide.Server) AddictTradeSystem.Instance?.CloseTradeFor(this);
    }

    private void DoMug(EntityAgent victim)
    {
        var player = victim as EntityPlayer;
        var hand = player?.ActiveHandItemSlot;
        bool robbedSomething = false;
        if (hand?.Itemstack != null)
        {
            int qty = hand.Itemstack.StackSize;
            var stolen = hand.TakeOut(qty);
            hand.MarkDirty();
            // The loot goes into the mugger's pockets, so catching and killing it gets it back.
            if (stolen != null && Pockets is { } p)
            {
                p.Add(World, stolen);
                SavePockets();
            }
            robbedSomething = true;
        }

        victim.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, MugDamage);

        Say(robbedSomething ? "vs-dope:addict-robbed" : "vs-dope:addict-mugged");
        BeginFlee();
    }

    private void AttackTarget(EntityPlayer target)
    {
        // Simple melee retaliation on a short cadence driven by the tick loop.
        if (ageSeconds - lastAttackAge < AttackCooldownSeconds) return;
        lastAttackAge = ageSeconds;
        target.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, RetaliateDamage);
    }

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

    private void BeginFlee()
    {
        if (state == AddictState.Flee) return;
        SetState(AddictState.Flee);
        StopMoving();
        AddictTradeSystem.Instance?.CloseTradeFor(this);
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

    private void MoveAway(double tx, double tz, float speed, bool sprint = false)
    {
        double dx = Pos.X - tx;
        double dz = Pos.Z - tz;
        double len = Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-3) { StopMoving(); return; }
        Walk(dx / len, dz / len, speed, sprint);
        Face(tx, tz); // keep eyes on the threat while backing off
    }

    // `speed` is the net WalkVector magnitude (see the tuning notes at the top).
    private void Walk(double dirX, double dirZ, float speed, bool sprint = false)
    {
        double scaled = speed * GlobalConstants.OverallSpeedMultiplier;
        if (sprint) scaled /= GlobalConstants.SprintSpeedMultiplier;
        ServerControls.WalkVector.Set(dirX * scaled, 0, dirZ * scaled);
        ServerControls.Forward = true;
        Controls.Forward = true;
        ServerControls.Sprint = sprint;
        Controls.Sprint = sprint;
    }

    private void StopMoving()
    {
        ServerControls.WalkVector.Set(0, 0, 0);
        ServerControls.Forward = false;
        ServerControls.Jump = false;
        ServerControls.Sprint = false;
        Controls.Forward = false;
        Controls.Jump = false;
        Controls.Sprint = false;
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
        (World.PlayerByUid(targetUid) as IServerPlayer)?.SendLocalisedMessage(0, langKey, Array.Empty<object>());
    }

    private void DespawnSelf()
    {
        if (World.Side != EnumAppSide.Server) return;
        AddictTradeSystem.Instance?.CloseTradeFor(this);
        (World as IServerWorldAccessor)?.DespawnEntity(this, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
    }
}
