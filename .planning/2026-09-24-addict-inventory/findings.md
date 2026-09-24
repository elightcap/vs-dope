# Findings — addict inventory / speed

## #38 speed: root cause (decompiled, 1.22.7)

Ground movement is `PModuleOnGround.DoApply` (VintagestoryAPI). Per 1/60 s sub-step:
`motion += WalkVector * GetWalkSpeedMultiplier(); motion *= (1 - 0.3)`. Then `PModuleMotionDrag`
scales by `0.983^(dt*33)` once per physics tick, and `EntityBehaviorControlledPhysics.ApplyTests`
moves the entity by `motion * dt * 60`.

- Server entities: `PhysicsManager` ticks at fixed `dt = 1/30` (2 sub-steps per tick).
  Steady state m = 2.249·w per tick, moved 2·m per 1/30 s => **speed ≈ 134.9 · w blocks/s**.
- Players: client `EntityBehaviorPlayerPhysics` ticks at `dt = 1/60`; `EntityControls.CalcMovementVectors`
  sets |WalkVector| = dt · BaseMoveSpeed(1.5) · MovespeedMultiplier(1) = 0.025. Steady state
  m = 2.263·w per 1/60 s => speed ≈ 135.8 · w. **Walk ≈ 3.39 b/s, sprint (x2 `SprintSpeedMultiplier`
  via `servercontrols.Sprint` in GetWalkSpeedMultiplier) ≈ 6.79 b/s**. `walkspeed` stat multiplies (default 1).
- Equivalent: player walk ≈ w 0.025, player sprint ≈ w 0.050 in entity WalkVector units.
- `ServerControls` and `Controls` are the *same object* on the server (`EntityAgent.Initialize`:
  `servercontrols = controls`), so there was no double application. The numbers themselves were
  simply too high: old WalkSpeed 0.04 = 5.4 b/s (faster than a walking player), FleeSpeed 0.055 =
  **7.4 b/s, faster than a sprinting player (6.8)**. LeaveSpeed 0.03 = 4.0 b/s.
- Non-player EntityAgent.GetWalkSpeedMultiplier: no walkspeed stat, only sneak/sprint controls and
  block multipliers (same for both, cancels out).

Fix: WalkSpeed 0.025 (3.4 b/s ≈ player walk), FleeSpeed 0.034 (4.6 b/s: outruns a walker by
1.2 b/s, a sprinter gains 2.2 b/s), LeaveSpeed 0.018 (2.4 b/s stroll).

## #40 design decisions

- **Storage**: `List<ItemStack>` in `Entities/AddictPockets.cs`, persisted in `Entity.Attributes`
  (server-only tree, saved by `Entity.ToBytes` when `!forClient`, not synced to clients) under
  `vs-dope-addict-inv` (`count` + `s0..sN` itemstack attributes). Reason: vanilla
  `EntityTradingHumanoid` uses an `InventoryTrader` mirrored into WatchedAttributes, but we don't need
  slot interaction or client sync (UI gets a packet), so a plain stack list is simpler.
  Loaded lazily on first server access (first OnGameTick / trade / death) so it always runs after
  `FromBytes` and after `Initialize`; stacks are re-resolved with `ResolveBlockOrItem`.
  A missing key = fresh addict => roll starting stock. Works for `/spawnaddict`, natural spawns and
  creative spawning alike.
- **Stock**: `AddictPockets` constants: gears 6-45 (`game:gear-rusty`), 2-4 junk kinds from a table
  of verified vanilla item codes (checked against `assets/game/lang/en.json` item keys), each with
  a gear-equivalent value; 30% chance of 1 small drug (opium 1-2 / morphine 1 / coca vitae 1).
  Unresolvable codes are skipped with a server log warning.
- **Payment**: price = units × unit price. Affordable units = floor((gears + junk value) / unit price).
  0 => refuse (`addict-cant-afford`); fewer than asked => sell what they can afford and say so.
  Pays gears first; any remainder in junk: repeatedly take the most valuable item that is <= remaining,
  else the cheapest item above remaining (small overpay, no change). Drugs (bought or starting) are
  never used as payment. Payment that does not fit in the player's inventory is dropped at their feet.
- **Liquids (heroin)**: stored as filled `game:jug-blue-fired` (vanilla `BlockLiquidContainerTopOpened`,
  3 L capacity) using `BlockLiquidContainerBase.TryPutLiquid`, topping up existing heroin jugs first.
  Reason: a loose `ItemLiquidPortion` stack is not a real inventory item (odd icon, can't be picked
  up usefully when dropped); a jug drops as a normal usable container. Solid drugs merge into stacks.
- **Death drops**: override `Entity.GetDrops` (only called from `Entity.Die` when reason == Death) to
  append the pockets and clear them. Covers player kills and `Overdose()` (which calls Die(Death)).
  `DespawnSelf` uses `EnumDespawnReason.Removed` => no drops.
- **Network**: `OpenAddictTradePacket` gained `PlayerHeld[]`, `PlayerGears`, `AddictGears`,
  `AddictGoodsValue`, `AddictStacks` (nested `AddictStackData { byte[] Stack }` from `ItemStack.ToBytes()`,
  rebuilt client-side with `new ItemStack(byte[])` + `ResolveBlockOrItem`) and `Refresh`.
  Nested contract avoids jagged `byte[][]` (not supported by protobuf-net). Registration order unchanged.
  After each sale the server re-sends the packet with `Refresh = true`; the client only applies a refresh
  to an already-open window for the same addict.
- **UI**: inspired by vanilla `GuiDialogTrader` ("{0} has {1} Gears" / "You have {0} Gears", slot grid).
  Pockets section = passive slot grid (8 per row) + gear/goods-value line; buy list = drug slot, name,
  "you have N", price, Sell 1 / Sell all; footer "You have N rusty gears" + Goodbye button.
