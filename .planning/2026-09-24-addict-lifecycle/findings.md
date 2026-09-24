# Findings

## Requirements
- #30 "addicts dont die": they should die after a certain amount of HP loss.
- #31 "addict doesnt leave after purchase": if they don't OD they should walk away and despawn when out of render.
- Coordinator: don't touch OverdoseSystem/AddictionSystem/DrugConsumableItem/statuseffects; don't deploy; persist the leaving state in WatchedAttributes; an addict that never trades must still be cleaned up as before.

## #30 root cause (verified by decompiling 1.22.7)
- The server-side health config was **fine**: `EntityBehaviorHealth` (VSEssentials) reads `maxhealth`/`currenthealth`, subtracts damage in `OnEntityReceiveDamage` and calls `entity.Die(Death)` at 0. The server attack path is `ServerSystemEntitySimulation.HandleEntityInteraction` -> `entity.OnInteract(Attack)` -> `EntityAgent.OnInteract` -> `ReceiveDamage`. So HP was never infinite.
- What made them look unkillable:
  1. `EntityDrugAddict.OnInteract` returned before `base` whenever `World.Side != Server`, **including for attacks**. `ClientMain.TryAttackEntity` calls `entity.OnInteract(Attack)` locally and then sends the packet. The local call is what plays the hit ("slap") sound for the attacker (the server's `PlaySoundAt(..., dualCallByPlayer)` skips that player), starts the client hurt animation/knockback feedback and calls `OnAttackingWith`. The attacker got no feedback at all.
  2. The shape `shapes/entity/drugaddict.json` had **no animations**, so there was no `hurt` and no `die`. There was also no `deaddecay`, and `EntityAgent.AllowDespawn` defaults to true, so an addict at 0 HP was removed the next tick instead of falling down.
  3. 20 HP against fists (0.5 dmg, 500 ms invulnerability after each hit) is about 40 hits while the addict hits back. Logs contain no "Player X killed vs-dope:drugaddict" audit line, so no test ever reached 0 HP.
- Fix: health 15 (frailer than a trader's 25), `deaddecay` hoursToDecay 3 (as used by vanilla `shiver`), `deadHitboxSize` (as used by the trader), client `textures.skin` + `animations` hurt/die (hare pattern), shape restructured into `root -> torso -> head` with `hurt` and `die` keyframes, and attacks call `base.OnInteract` on both sides.
- `EntityCubeParticles.Init` (spawned by `deaddecay.DecayNow`) dereferences `Properties.Client.FirstTexture.Baked` on the client. `FirstTexture` is null unless the entity JSON declares client `textures`, so the `textures` block is **required** or the decay would NRE on the client.
- `EntityBehaviorDeadDecay.OnEntityDeath`: `DamageSource.Source == 5` (`EnumDamageSource.Void`) sets AllowDespawn = true (no corpse). Overdose therefore uses `Internal`.

## #31 design decisions
| Decision | Reason |
|---|---|
| Manual steering `Leave` state, not `taskai`/AiTaskBase | The entity already steers by hand and has no taskai behavior. Mixing both would fight over controls, and the csproj doesn't reference VSEssentials. |
| Linger 10 s after each sale, then walk | Leaving on the first sale used to kill later sales in the same window (see the old comment). A linger that each sale resets keeps multi-item selling working. |
| Server sends `CloseAddictTradePacket` when the addict walks off, flees, dies or despawns | Otherwise the client window dangles and every click fails. |
| Despawn when no online player "can see" it (none within 24 blocks; none within 64 blocks with the addict inside a ~60 degree view cone) | Out-of-tracking-range (`IsTracked == 0`) can't be used: past the simulation range (128) the entity stops ticking entirely, so it could never despawn itself. |
| Fallback timeout 1 in-game hour, measured with `Calendar.TotalHours` stored at walk start | Survives reloads, unlike the runtime `ageSeconds`. |
| Persist state/engaged/friendly/heading/start-hours in WatchedAttributes | Required by the task. A reloaded leaving addict keeps walking and doesn't linger again. |
| Target offline/dead -> `Leave` (was: `Flee` with no target, which stood still until the 900 s cap) | Cleaner cleanup. The never-traded paths (give up, impatience flee, mug flee, 900 s cap) are unchanged. |

## API facts checked (1.22.7)
- `EntityAgent.ShouldDespawn => !Alive && AllowDespawn`. `AllowDespawn` defaults true, and `EntityBehaviorDeadDecay.Initialize` sets it false.
- `EntityBehaviorDespawn` only acts while alive and uses `NearestPlayerDistance` (server, PhysicsManager).
- `EntityPos.GetViewVector()` = (-cos p * sin y, sin p, -cos p * cos y).
- `IWorldAccessor.AllOnlinePlayers` can include connecting players, so filter on `IServerPlayer.ConnectionState == Playing`.
- Entity JSON: `deadHitboxSize` sets both dead collision and dead selection boxes (`EntityType.DeadHitBoxSize`).
- Shape children: `from`/`to`/`rotationOrigin` are relative to the parent's `from`.
