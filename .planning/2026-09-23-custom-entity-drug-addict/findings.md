# Findings & Decisions

## Requirements (from user)

- New custom entity: "drug addict" NPC.
- Built on the EntityAgent foundation (user's explicit request).
- Spawns randomly: 0–3 times per in-game day ("maybe not at all").
- Wants to BUY drugs from the player.
- Has a chance of trying to rob/attack the player.

## Research Findings

### Vanilla trader architecture (VS 1.22.7, /opt/vintagestory)

- `assets/survival/entities/humanoid/trader-male.json`: `"code": "trader"`, `"class": "EntityTrader"` — vanilla traders are a game-side class (in VintagestoryLib.dll, NOT the mod API).
  - Renderer: `Shape` with humanoid shape + outfit system (`outfitConfigFileName`), variantgroups gender/type/climate.
  - Trading driven by `attributes.tradePropsFile: "config/tradelists/trader-{type}"` and client behavior `conversable` with `dialogueByType` → `config/dialogue/trader.json`.
  - Client behaviors used: nametag, repulseagents, controlledphysics (stepHeight 1.01), interpolateposition, conversable.
- Dialogue config lives at `/opt/vintagestory/assets/survival/config/dialogue/trader.json`.

### API surface (VintagestoryAPI.dll / VintagestoryAPI.xml)

- `Vintagestory.API.Common.EntityAgent` is the public API base class for agent-like entities; mods subclass it directly (EntityAI/TaskAI system is game-side only, not in API).
- Registration: API exposes `RegisterEntity` (+ `RegisterEntityBehaviorClass`, `RegisterEntityRendererClass`). Custom entity classes are registered in code and referenced from entitytype JSON via `"class"`.
- Spawning: `ISingletonUtil`/world API provides `TrySpawnEntity`; also natural-spawn hook `add_OnTrySpawnEntity` (entity JSON `weight` + tags drive vanilla spawn manager).
- Since TaskAI isn't in the API, custom AI = override server tick logic on our EntityAgent subclass (+ custom EntityBehavior classes if needed).

### Repo state (/home/evan/git/vs-dope)

- No existing entity code. Systems: AddictionSystem (hourly tick — good hook for daily spawn rolls), SyringeRecipeSystem. `src/Behaviors/` exists but is empty.
- Drug item codes exist under assets/vs-dope/itemtypes (opium, morphine, heroin etc., per docs/repo maps).

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| Subclass `Vintagestory.API.Common.EntityAgent` directly | User asked for EntityAgent foundation; game-side EntityAI/TaskAI not available to mods |
| Custom spawn ModSystem (not vanilla spawn manager) | Need controlled 0–3/day cadence near player, not density-based natural spawns |
| Reuse vanilla trader humanoid shape/outfit assets if possible | Fastest path to a believable humanoid; custom art deferred |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| EntityTrader/EntityAI internals not documented in API XML (game-side) | Confirmed via strings on DLLs that mods use RegisterEntity + own tick logic; will design AI manually |

## User Answers (Phase 1 gate — RESOLVED)

- **Spawn**: appear near the player (20–40 blocks), then pathfind/steer toward the player after spawning.
- **Trade direction**: addict only BUYS drugs from the player; sells nothing. Pays above market price.
- **Robbery trigger**: a one-time yes/no roll made at spawn time; robbery does NOT begin until dialogue/interaction starts (i.e., when the player engages). So an "honest" addict trades, a "desperate" one mugs you once engaged.
- **Steal target**: drug items only; moderate threat/damage; always flees after theft; can be killed if caught/fought.
- **Despawn**: despawns once out of chunk range (EnumDespawnReason.OutOfRange).
- **Appearance**: vanilla trader/humanoid model with a ragged/poor outfit subset + sickly tint (reuse vanilla humanoid assets, no custom art yet).
- **Overdose**: after a purchase completes, there is a chance the addict overdoses and dies on the spot.

## Compile-Critical API Signatures (verified via reflection)

- Register: `ICoreAPICommon.RegisterEntity(string className, Type entity)` — call in `ModSystem.Start()`. JSON `"class"` references this string.
- Entity overrides available to mods:
  - `Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)` (param is `Int64` chunk index)
  - `OnGameTick(float dt)` — server AI tick
  - `bool ReceiveDamage(DamageSource damageSource, float damage)`
  - `Die(EnumDespawnReason reason, DamageSource damageSourceForDeath)`
  - `bool TryGiveItemStack(ItemStack itemstack)`
  - EntityAgent: `DidAttack(DamageSource source, EntityAgent targetEntity)`, `OnHurt(DamageSource, float)`
  - EntityAgent: `OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)` (mode ∈ { Attack, Interact })
- Movement/pos: `Pos` is `EntityPos` (`SetPos(x,y,z)`, `.X/.Y/.Z`, `Copy()`, `Yaw`); `BodyYaw`/`BodyYawServer` settable; `Alive` settable bool. `Controls`/`ServerControls` are `EntityControls` with bool flags Forward/Backward/Left/Right/Jump/Sprint/Sneak.
- Spawn: `IWorldAccessor.SpawnEntity(Entity)`, `.AllOnlinePlayers`, `.NearestPlayer(x,y,z)`, `.GetEntitiesAround(Vec3d, horRange, vertRange, matches)`.
- `DamageSource` fields: `Source (EnumDamageSource)`, `Type (EnumDamageType)`, `SourceEntity`, `CauseEntity`, `DamageTier`, `KnockbackStrength`. Parameterless ctor.
- `EnumDespawnReason`: Death, Combusted, OutOfRange, PickedUp, Unload, Disconnect, Expire, Removed.
- No public pathfinder on EntityAgent → implement manual steering: set BodyYaw toward target + Controls.Forward; jump when blocked. (No TaskAI in API.)

## Design (Phase 2 — AI state machine)

States: `Spawned` → `ApproachPlayer` (steer to player, stop ~3 blocks) → `Idle/WaitForInteract`.
On player Interact: if desperate(robbery roll=true at spawn) → `Mug` (demand; on refuse or after steal attempt → attack with melee DamageSource, steal drug items from target inventory via TryGiveItemStack-to-self/remove-from-player, then `Flee`). Else → `Trade` (open trade interaction; player hands over drugs for coins).
After a successful purchase: roll overdose chance → if hit, apply lethal self damage (`Die(EnumDespawnReason.Death)` + emote/particles).
Each tick: if no online player within despawn range and far from spawn chunk → `Despawn(OutOfRange)`.

## Trade interaction approach (decision)

Full vanilla trader GUI is game-side and not exposed. v1 uses a lightweight custom flow: on Interact, addict opens chat prompt + accepts the drug item currently in the player's active hand for coins above market price (single-item sale per interact), OR a minimal client dialog via `GuiDialog` if needed. Keep server-authoritative.

## Pivot Research (2026-09-24 — trader UI)

- **Real trader window is game-side**: no `OpenTraderDialog`/trade API in `VintagestoryAPI.xml`. Only `GuiTrading` (not exposed). So a mod-built trader UI must use `GuiDialog`.
- **Custom dialog API**: `Vintagestory.API.Client.GuiDialog` / `GuiDialogGeneric` (ctor exposed), with `TryOpen`/`TryClose`, `DlgComposers`, slot + button composers, `OnMouseClickSlot`, `OnGuiClosed`. Opened on client; server↔client sync via network channels.
- **Networking**: `INetworkAPI.RegisterChannel` on both `IClientNetworkAPI` and `IServerNetworkAPI`; server sends to a specific player, client sends request back. Standard pattern for a mod shop window.
- **Currency = rusty gears**: real VS currency item is `game:gear-rusty` (`assets/survival/itemtypes/resource/gear.json`, code `gear` + variant `rusty`, stack 1000, `attributes.currency.value:1`). Use this instead of ingots for payment.
- **Design decisions (user-confirmed)**: custom trader-style dialog; addict only buys drugs (player sells); random chance trade turns hostile; pay in rusty gears above market.

## Resources

- `/opt/vintagestory/assets/survival/entities/humanoid/trader-male.json` — reference entity JSON
- `/opt/vintagestory/assets/survival/config/dialogue/trader.json` — dialogue format
- `/opt/vintagestory/VintagestoryAPI.xml` — `T:Vintagestory.API.Common.EntityAgent`
- docs/repo/RUNTIME.md, DEPENDENCIES.md — mod subsystem maps
