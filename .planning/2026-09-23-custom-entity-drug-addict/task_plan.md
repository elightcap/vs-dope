# Task Plan: Custom "Drug Addict" Entity (EntityAgent-based)

## Goal

Add a custom NPC entity ("drug addict") on the EntityAgent foundation that spawns randomly (0–3/day), buys drugs from the player through a **trader-style GUI window** (not chat roleplay), pays in **rusty gears** (`game:gear-rusty`) above market, and has a random chance of turning hostile to rob/attack.

## Next Step

Deploy to `~/.config/VintagestoryData/Mods/vs-dope/`, restart game, and playtest: `/spawnaddict`, right-click an addict → trader dialog should open (friendly) or it mugs you (hostile). Sell drugs → receive rusty gears; watch for overdose/flee. Then update docs/repo maps and commit on a fresh branch (ask before push).

## Current Phase

Phase 7: Build & Playtest (trader UI) — code complete, build green; awaiting in-game playtest

## PIVOT (2026-09-24)

User rejected the chat-prompt/active-hand "roleplay" trade. New direction (confirmed via questions):
- **Custom trader-style `GuiDialog`** window on the addict (real `GuiTrading` is game-side, not in mod API).
- Addict **only buys drugs** from the player (player sells to addict).
- **Random chance the trade turns hostile** (closes window → attack → steal → flee).
- Currency = **rusty gears** (`game:gear-rusty`), above market price.

## Phases

### Phase 1: Requirements & Discovery

- [x] Research EntityAgent / EntityAI / trader entity architecture in VS API and vanilla assets
- [x] Determine how to register a custom non-block entity (entitytype JSON + class registration)
- [x] Ask user clarifying questions (spawn mechanics, trades, aggression, appearance, despawn)
- [x] Document findings in findings.md
- **Status:** complete

### Phase 2: Design & Structure

- [x] Decide entity class hierarchy (EntityAgent subclass + manual steering; TaskAI not in API)
- [x] Design spawn system (ModSystem ticking day clock, roll per-day count 0–3, near-player placement)
- [x] Design trade interaction (chat-prompt / active-hand sale for coins above market price)
- [x] Design robbery/attack behavior (one-time roll at spawn; engage on interact → steal drugs → flee)
- **Status:** complete

### Phase 3: Implementation

- [x] Entity type JSON + shape/textures (custom simple humanoid box shape + solid-color placeholder PNG)
- [x] C# entity class with AI behaviors and trade logic (`src/Entities/EntityDrugAddict.cs`)
- [x] Spawn ModSystem with daily random spawn rolls (`src/Systems/DrugAddictSpawnSystem.cs`)
- [x] Localization entries (entity name + dialogue keys in `assets/vs-dope/lang/en.json`)
- **Status:** complete

### Phase 4: Testing & Verification

- [x] `dotnet build` clean (0 errors)
- [ ] Deploy to mods folder; verify entity loads without crashes
- [ ] Verify spawn cadence, trade window, and attack behavior in-game (user playtest)
- **Status:** in_progress

### Phase 5: Delivery (deferred until trader UI lands)

- [ ] Review all files; update docs/repo maps (RUNTIME.md / DEPENDENCIES.md) for new subsystem
- [ ] Commit on a fresh branch; ask user before pushing
- **Status:** pending

### Phase 6: Trader-UI Redesign — Network + Dialog (NEW)

- [x] Register client+server network channels (`vs-dope.addicttrade`, both message types) — server in `AddictTradeSystem.Initialize`, client in `AddictTradeUiSystem.StartClientSide`
- [x] Client `GuiDialog` subclass (`GuiDialogAddictTrade`): per-drug row = passive item-slot icon + name + gear price + Sell 1 / Sell all buttons
- [x] Server: on Interact roll hostile (35%); friendly → `OpenTradeFor` sends open-dialog packet (per-drug gear prices); hostile → existing `DoMug` attack/steal/flee
- [x] Server handler `OnSell`: validate drug, take from player inventory, pay `game:gear-rusty` (chunks of stack max), 12% overdose roll, else flee
- [x] Refactored `EntityDrugAddict`: added `Friendly`, `TargetPlayerUid`, `OnPurchaseCompleted`, `Overdose`, `BeginFleeNow`; removed active-hand chat flow (`DoTrade`/`PayFor`/`DrugPathOf`)
- [x] Localization: added `vs-dope:addict-overdose`
- **Status:** complete

### Phase 7: Build & Playtest (trader UI)

- [x] `dotnet build` clean (0 errors)
- [ ] Deploy + verify dialog opens on interact, sells drugs for gears, hostile roll works (user playtest)
- **Status:** in_progress

## Key Questions

1. Spawn style: wander up near the player (wandering-trader-like), or natural world spawn?
2. Does the addict also SELL anything, or only buy drugs?
3. Robbery trigger: on interaction chance, random while nearby, or when refused a sale?
4. Steal target: only drug items, or anything from inventory? Combat damage level?
5. Appearance: reuse vanilla trader/humanoid model+textures with palette swap, or custom art later?
6. Despawn: linger forever, wander off after N minutes, until killed?

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Base on EntityAgent per user request | User explicitly asked for the EntityAgent foundation |
| Manual steering via ServerControls.WalkVector + controlledphysics behavior | TaskAI not in API; controls-based movement keeps gravity/collision correct (no manual pos lerp) |
| Custom simple humanoid box shape + solid-color placeholder PNG | Avoids crash risk of reusing vanilla trader outfit; matches repo's placeholder-art convention |
| Payment as dropped metal ingot item entities at buyer position | Simple, visible "above market" reward without touching player inventory APIs |
| Drug detection on ActiveHandItemSlot only | Simpler + predictable interaction (hold the drug to sell) vs full-inventory scan |

## Errors Encountered

| Error | Attempt | Resolution |
|-------|---------|------------|
| `OnDeath()` no suitable method to override | 1 | Removed the OnDeath override (not an overridable member on EntityAgent) |
| double literal → float const | 1 | Added `f` suffix (`2.4f`) |
| `.Position` / bare `Position` not found | 1 | Entity position is `.Pos` (EntityPos with settable X/Y/Z doubles); replaced all |
| ItemSlot has no `.ItemStack` | 1 | Property is `.Itemstack` (lowercase s) |
| DamageSource has no `.SourceEntityId` | 1 | Set `SourceEntity = this`; compare via `damageSource.SourceEntity?.EntityId` |
| SyncedTreeAttribute.HasKey not found | 1 | Use base TreeAttribute method `HasAttribute(key)` |
| IBlockAccessor.GetChunkAtMapPosBlock missing | 1 | Replaced with `GetMapChunkAtBlockPos(new BlockPos(x,0,z)) == null` loaded-column guard + groundY>1 check |

## Notes

- VS API reference: Vintagestory.API, vanilla assets at /opt/vintagestory/assets (per AGENTS.md crop reference path).
- Existing mod systems in src/Systems follow ModSystem pattern; new spawn system should match.
