# Progress Log

## Session 2026-09-23

1. Initialized plan `custom-entity-drug-addict`.
2. Filled task_plan.md with goal, phases (5), and key open questions for the drug-addict NPC.
3. Explored src/: no existing entity code; mod currently has Items/, Systems/ (AddictionSystem, SyringeRecipeSystem), Client/, Behaviors/ (empty). EntityAgent only referenced as parameter type in item classes so far.
4. Researched vanilla trader: `class: "EntityTrader"` is game-side (not mod API); mods subclass `Vintagestory.API.Common.EntityAgent` and register via `RegisterEntity`; TaskAI unavailable → custom tick-based AI needed. Findings logged in findings.md.
5. Asked user clarifying questions (spawn style, trades both directions?, robbery trigger/loot, appearance, despawn).
6. Wrote implementation: `src/Entities/EntityDrugAddict.cs` (state machine Approach→WaitInteract→Trade/Mug/Flee; manual WalkVector steering), `src/Systems/DrugAddictSpawnSystem.cs` (day-change roll 0–3 near players), registered entity + init in VsDopeModSystem.
7. Added assets: `entitytypes/drugaddict.json`, custom humanoid box `shapes/entity/drugaddict.json`, placeholder texture `textures/entity/drugaddict.png` (16x32 solid color), dialogue lang keys.
8. Fixed compile errors via API reflection (see Error Log): Position→Pos, .ItemStack→.Itemstack, SourceEntityId→SourceEntity, HasKey→HasAttribute, chunk-loaded guard → GetMapChunkAtBlockPos null-check. **Build succeeded (0 errors).** All JSON validated; PNG is power-of-two.
9. Added debug slash command `/spawnaddict [count]` (server-side, privilege "dev") in DrugAddictSpawnSystem — spawns 1-20 addicts ~6-14 blocks from the caller for fast testing. Refactored spawn into reusable `SpawnNear(player,count,minOff,maxOff)`. Build green again.

## Error Log

| Timestamp | Error | Attempt | Resolution |
|-----------|-------|---------|------------|
| 2026-09-23 | OnDeath() no method to override | 1 | Removed override (not on EntityAgent) |
| 2026-09-23 | double→float const literal | 1 | `f` suffix |
| 2026-09-23 | `.Position`/bare Position missing | 1 | Use `.Pos` (EntityPos X/Y/Z doubles) |
| 2026-09-23 | ItemSlot.ItemStack not found | 1 | Property is `.Itemstack` (lowercase s) |
| 2026-09-23 | DamageSource.SourceEntityId missing | 1 | Set `SourceEntity = this`; compare `SourceEntity?.EntityId` |
| 2026-09-23 | SyncedTreeAttribute.HasKey missing | 1 | Use TreeAttribute.HasAttribute(key) |
| 2026-09-23 | IBlockAccessor.GetChunkAtMapPosBlock missing | 1 | GetMapChunkAtBlockPos(BlockPos)==null guard + groundY>1 |

## 5-Question Reboot Check

| Question | Answer |
|----------|--------|
| Where am I? | Phase 4: Testing & Verification — build is green, needs in-game playtest |
| Where am I going? | Deploy to Mods folder and verify spawn/trade/rob behavior live; then docs + commit |
| What's the goal? | Custom entity on EntityAgent foundation; 0–3 spawns/day; buys drugs; chance to attack |
| What have I learned? | See findings.md + task_plan Errors table for exact API member names (Pos, .Itemstack, SourceEntity) |
| What have I done? | Full implementation written; `dotnet build` clean; JSON/PNG validated |

---

*Update this file after completing a phase, running validation, or encountering an error.*

| 2026-09-24 | entity asset silently not loaded (0 matches in registry, no load error) | 1 | Entity type JSONs live in assets/<modid>/entities/ NOT entitytypes/. Moved drugaddict.json to entities/. Redeployed. |

## Session 2026-09-24 (trader-UI pivot)

1. User rejected chat-prompt trade → wants a trader-style GUI window. Asked 4 questions; answers: custom GuiDialog, addict only buys drugs, random chance trade turns hostile, currency = rusty gears.
2. Verified currency: `game:gear-rusty` (code `gear` + variant `rusty`, stack 1000, `currency.value:1`) — real VS money. No vanilla "rusty gear" needed to create; it exists.
3. Confirmed API surface: real `GuiTrading` is game-side (not in API). Use `GuiDialog`/`GuiDialogGeneric`. Network: `RegisterChannel` (client+server) → channel `.RegisterMessageType<T>()`, `.SetMessageHandler<T>()`, server `.SendPacket(player, obj)`, client `.SendPacket(obj)`. Composer helpers: `AddButton(text, ActionConsumable, bounds, font, style, id)`, `AddSmallButton`, `AddPassiveItemSlot(bounds, IInventory, ItemSlot, bool, id)`, `AddDialogTitleBarWithBg(title, Action close, font, bounds, id)`.
4. **Baseline build green** with `export VINTAGE_STORY=/opt/vintagestory` (env was empty → 133 spurious errors). Remember to set it every build.
5. Existing repo GuiDialog reference: `src/Client/CocaVitaeEffectHudSystem.cs` (`GuiDialogCocaVitaeEffect`) — mirror its Compose()/TryOpen() pattern.
6. Wrote the trader-UI flow end-to-end: `src/Network/AddictTradePackets.cs` (OpenAddictTradePacket S→C with long AddictEntityId + parallel DrugCodes/GearPrices arrays; SellToAddictPacket C→S, Quantity<=0 = all), `src/Systems/AddictTradeSystem.cs` (server backend: offer table opium12/morphine30/coca-vitae40/heroin75, channel `vs-dope.addicttrade`, OpenTradeFor/OnSell, CountDrug/TakeDrug scan player InventoriesOrdered, gear payment in stack-sized chunks, 12% overdose), `src/Client/AddictTradeUiSystem.cs` (client channel + `GuiDialogAddictTrade` with per-drug `InventoryGeneric(1,..)` display slots + Sell 1/Sell all buttons). Rewired `EntityDrugAddict.OnInteract` (35% hostile roll) and added `Friendly`/`TargetPlayerUid`/`OnPurchaseCompleted`/`Overdose`/`BeginFleeNow`. Wired `AddictTradeSystem.Initialize` in `VsDopeModSystem.StartServerSide`. **Build green (0 errors).**
7. Reflection findings (VS 1.22 API, verified via net10 reflection probe on VintagestoryAPI.dll):
   - `EnumButtonStyle` members = `None, MainMenu, Normal, Small` (NO `Default`/`SmallFlat`).
   - `ActionConsumable` = `delegate bool ActionConsumable()` — parameterless, returns bool (consume).
   - Client `SetMessageHandler<T>` takes `NetworkServerMessageHandler<T>` whose doc shows only `packet`; use a 1-arg lambda `(packet) => ...`.
   - Server `IServerNetworkChannel.SendPacket<T>(T packet, IServerPlayer[] targets)` — packet FIRST, players array second (not `SendPacket(player, packet)`).
   - `EnumChatType` has no `Error`; use `Notification`/`CommandError`.
   - `ItemSlot` exposes the stack via `.Itemstack` (then `.Item`, `.StackSize`); `ItemSlot.TakeOut(n)` is on the slot.
   - Display inventories for icons: `new InventoryGeneric(int size, string code, ICoreAPI api, NewSlotDelegate = null)`; set `inv[0].Itemstack`.
   - `CairoFont` size helper is `.WithFontSize(float)` (not `WithSize`).
   - `Entity.EntityId` is `long` → packet id fields must be long.
   - Reflection probe recipe (for future API lookups): net10 console app, `AllowMissingPrunePackageData=true`, `UseAppHost=false`, `AssemblyResolve` → `/opt/vintagestory/<name>.dll`, then `asm.GetType(full, true)` + `Enum.GetNames`/`GetMethod("Invoke")`. Run with `dotnet exec bin/Debug/net10.0/<app>.dll`.

## Session 2026-09-24 (trader UI never opened — root cause & fix)

User reported the trader window does not work. Three defects found by decompiling the
game DLLs (`ilspycmd`, installed globally) rather than by playtesting:

1. **`EntityPlayer as IServerPlayer` is always null.** `EntityDrugAddict.OnInteract`
   passed `player as IServerPlayer` to `AddictTradeSystem.OpenTradeFor`. Decompiled
   `Vintagestory.API.Common.EntityPlayer` is `EntityHumanoid, IPettable` — it does *not*
   implement `IServerPlayer`, so the cast silently yielded null and `OpenTradeFor`'s
   null-guard returned immediately. Compiles fine (`as` to an interface from a
   non-sealed class). **Fix:** `World.PlayerByUid(entityPlayer.PlayerUID) as IServerPlayer`
   via the new `ServerPlayerOf()` helper.
2. **Packets had no protobuf contract.** `Vintagestory.Server.NetworkChannel.GenPacket`
   calls `ProtoBuf.Serializer.Serialize<T>` directly with no model setup, and VS ships
   protobuf-net **2.4.0**, which throws
   `InvalidOperationException: Type is not expected, and no contract can be inferred`
   for an unattributed POCO. Verified empirically against `/opt/vintagestory/Lib/protobuf-net.dll`.
   **Fix:** `[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]` on both packets,
   plus a `protobuf-net` reference (`$(VINTAGE_STORY)/Lib/protobuf-net.dll`) in the csproj.
   Round-trip re-verified against the built DLL: 38 B / 30 B, all fields intact.
3. **Spawned addicts had no target.** `BindTarget` wrote the watched attribute only
   `if (WatchedAttributes.HasAttribute(...))` — inverted guard, so it never persisted.
   `ServerMain.SpawnEntity_internal` calls `entity.Initialize()` *after* the spawn system
   calls `BindTarget()`, and `Initialize` unconditionally re-read the (absent) attribute,
   wiping `targetUid`. Result: first tick sees a null target → `BeginFlee` → the addict
   stands still and never approaches or greets anyone. **Fix:** `SetTarget()` always
   persists; `Initialize` only adopts the stored uid when nothing is bound yet.

### Verified non-issues (checked, do not re-investigate)

- `ElementBounds.Fill` is a **property** returning a fresh instance — `.WithFixedSize()` on
  it does not corrupt shared state.
- `GuiDialog.TryOpen()` self-registers; `capi.Gui.RegisterDialog()` is only needed for
  dialogs that must listen to events while closed.
- The 4-arg `Entity.OnInteract(EntityAgent, ItemSlot, Vec3d, EnumInteractMode)` override is
  still the current API in 1.22.7; `Entity.IsInteractable` defaults to true.
- `Entity.ServerPos => Pos` (same object) in 1.22.7, so `ServerPos.SetPos()` before spawn is
  correct — but `ServerPos` is now `[Obsolete]`, prefer `Pos`.
- `InventoryBase(string invId, ...)` does `invId.Split('-', 2)` and would throw on an id with
  no hyphen; `"vs-dope:addictdisplay<i>"` happens to contain one.
- Item codes `vs-dope:opium|morphine|coca-vitae|heroin` all exist, unvariated.
- All `vs-dope:addict-*` lang keys are present in `assets/vs-dope/lang/en.json`.

### Status

Build green, deployed to `~/.config/VintagestoryData/Mods/vs-dope`. **Needs playtest:**
restart the game (DLL change), `/spawnaddict`, confirm the addict walks to you and greets,
then right-click → trader window with 4 rows → Sell 1 / Sell all → rusty gears.

## Session 2026-09-24 (playtest round 2 — three reported faults)

User playtested and reported: only opium sells; opening any other UI permanently breaks
the addict window; the entity sprints unnaturally fast. All three traced and fixed.

### 1. "Only selling opium works" — caused by the flee-after-every-sale rule

`OnSell` ended every successful sale with `addict.BeginFleeNow()`. Combined with fault 3
(24x movement speed) the addict crossed the 30-block `FleeDistance` in about a second and
hit `DespawnSelf()`. The next sale then found `GetEntityById` == null and **returned
silently**, so every row after the first looked like a dead button. Opium is simply row 0.

**Fixes:** the addict now stays and keeps buying (`OnPurchaseCompleted` resets it to
`WaitInteract`); it leaves only on impatience, overdose or a mugging. `BeginFleeNow()`
removed (no callers). Every rejection path in `OnSell` now reports to the player instead of
returning silently — the silence is what disguised this.

### 2. Heroin was unsellable by construction

`heroin.json` is `class: ItemLiquidPortion`, `matterState: liquid`, stack 5000 — it lives
in liquid containers, never as a loose inventory stack, so the slot scan could never find
it. `AddictTradeSystem` now detects liquid offers via
`BlockLiquidContainerBase.GetContainableProps(stack) != null` (lazily, since item types
aren't registered at `Initialize` time) and counts/takes them **per litre** out of any
carried `BlockLiquidContainerBase` using `GetCurrentLitres` / `TryTakeContent`. The open
packet carries a per-row `Units` array so the window shows `75g /L` vs `12g ea`.

Also fixed `heroin.json` shape `item/liquid` → `game:item/liquid`. Unqualified shape paths
resolve to the *mod's own* domain, and `vs-dope:shapes/item/liquid.json` does not exist;
vanilla's is `assets/survival/shapes/item/liquid.json` (assets/survival = domain `game`).

Inventory scan narrowed from `InventoriesOrdered` (which includes the **creative**
inventory — one of every item — plus mouse/ground/crafting slots) to hotbar + backpack.

### 3. Another UI over the window bricked it permanently

`GuiDialog.TryOpen()` only calls `capi.Gui.TriggerDialogOpened(this)` on a false→true
transition of its protected `opened` flag, and that trigger is the **only** thing that puts
a dialog into `ClientMain.OpenedGuis` (the rendered list). `ClientMain.UnregisterDialog()`
removes a dialog from `LoadedGuis` *and* `OpenedGuis` without clearing `opened`. Once those
disagree, `TryOpen()` silently no-ops forever. `GuiDialogAddictTrade.Open()` now calls
`TryClose()` first when already open, forcing the transition every time.

Contributing factor: `InteractWindowSeconds = 45` applied while the trade window was open,
so browsing your bags for 45s made the addict wander off. A friendly (dealing) addict now
gets `TradingWindowSeconds = 240`, and `MaxLifetimeSeconds` went 360 → 900.

### 4. Movement speed

`WalkVector` is a **unit direction scaled by a move speed**, exactly as Essentials'
`StraightLineTraverser` does it (`WalkVector.Set(unit); WalkVector.Mul(speed *
GlobalConstants.OverallSpeedMultiplier)`). Vanilla reference speeds from entity JSON:
trader/villager walk **0.035**, trader stroll 0.01, wolf chase **0.052**, wolf walk 0.045.
The mod used **0.6** (and 0.84 fleeing) — ~17-24x. Now `WalkSpeed = 0.04`,
`FleeSpeed = 0.055`, both multiplied by `OverallSpeedMultiplier`. `Forward` is now set on
`Controls`/`ServerControls` while moving, matching vanilla.

### Status

Build green, packets re-verified through protobuf round-trip against the built DLL
(96 B / 30 B, all fields intact), deployed. **Needs playtest:** restart, `/spawnaddict`,
check walk speed looks human, sell several *different* drugs from one window, open the
inventory over the window and confirm it still reopens, and sell heroin from a bucket.

## Session 2026-09-24 (round 3 — right-click did nothing)

Logs from the user's run settled it without guesswork
(`~/.config/VintagestoryData/Logs/`):

- `client-chat.log` showed the spawn **and the greet line** at 10:49:02, proving spawn,
  target binding, approach and `Say()` all work after the round-1 fixes.
- No second greet, no mug line, no trade window → `EntityDrugAddict.OnInteract` was never
  reached on the server at all.
- `server-debug.log` join line showed the active hotbar slot held `vs-dope:syringe-empty`.

**Root cause: the mod's own held items swallow the right-click.**
`SystemMouseInWorldInteractions.HandleMouseInteractionsNoBlockSelected` gates entity
interaction on the held item declining the click:

```csharp
if (handling3 == EnumHandling.PassThrough
    && !TryBeginUseActiveSlotItem(null, game.EntitySelection)   // <-- held item claims it
    && game.EntitySelection != null)
{
    game.EntitySelection.Entity.OnInteract(...);                 // never runs
    game.SendPacketClient(ClientPackets.EntityInteraction(...)); // packet never sent
}
```

`TryBeginUseActiveSlotItem` returns true whenever `OnHeldUseStart` leaves `handling !=
NotHandled`. Both mod items set it **unconditionally**:
`DrugConsumableItem.OnHeldInteractStart` set `PreventDefault` as its last statement, and
`SyringeItem.OnHeldInteractStart` set it as its *first* statement, ahead of every
early-return — so even an empty syringe, or a stack of more than one, ate the click and did
nothing visible. Holding a drug or a syringe therefore made the addict completely
uninteractable, which is the exact test a player performs (you hold the drugs you came to
sell).

**Fix:** both handlers now return without claiming the click when
`entitySel?.Entity?.IsInteractable == true`. Safe because the client's own selection filter
is `e.IsInteractable && e.EntityId != player.EntityId`, and `EntityItem.IsInteractable` is
false, so dropped items never suppress eating. Syringe behaviour is unaffected: it only ever
injects `byEntity` (self) and never reads `entitySel`.

**Side effect to be aware of:** you can no longer eat a drug while aiming directly at an
interactable entity. Aim at anything else and it works normally.

Also added a `World.Logger.VerboseDebug` line at the top of `OnInteract`. If the window ever
fails again, grep `server-debug.log` for `[vs-dope] addict` — present means the click
reached the server and the problem is downstream; absent means the client never sent it.

### Entity interaction reference (verified, do not re-derive)

- Entity interaction is sent **only** when `game.BlockSelection == null`; otherwise
  `HandleMouseInteractionsBlockSelected` runs and has no entity path.
- Client selection filter: `e.IsInteractable && e.EntityId != game.EntityPlayer.EntityId`.
- `Entity.IsInteractable` defaults true; `EntityItem` overrides it to false.
- `CollectibleObject.OnHeldUseStart` dispatches to `OnHeldInteractStart` /
  `OnHeldAttackStart` by `useType`, so `OnHeldInteractStart` is the correct hook.
