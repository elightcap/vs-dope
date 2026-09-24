# Runtime Architecture

## Mod entry point

`src/VsDopeModSystem.cs` registers custom item classes used by JSON, creates `AddictionSystem` server-side, and exposes the static system reference used by consumables.

If a JSON `class` changes or a new custom item class is introduced, inspect registration here.

## Consumables

`src/Items/DrugConsumableItem.cs` is the primary path for directly consumed custom items. It owns held interaction, consumption, healing, intoxication/psychedelic attributes, movement modifiers, expiration, addiction recording, and product-specific tolerance.

Current subclasses:
- `OpiumItem`
- `MorphineItem`
- `CocaVitaeItem`

Tolerance is keyed by `ToleranceProduct`. Keep product IDs stable unless intentionally resetting/migrating stored player tolerance.

Coca Vitae writes its effect expiry to watched attributes for its client HUD. Its movement boost expires by comparing the calendar's total hours, so it lasts exactly one in-game hour even if the calendar speed changes.

Heroin writes `vs-dope-heroin-slow-expires-gamehour` on consumption. The addiction system removes its movement penalty after one in-game calendar hour rather than when the psychedelic attribute ends.

## Addiction and tolerance

`src/Systems/AddictionSystem.cs` owns addiction level, consecutive-use tracking, withdrawal, decay, product-specific tolerance, tolerance recovery, and heroin dose effects.

Tolerance behavior:
- first 2 uses of a product per in-game day do not add tolerance;
- heavier same-day use adds progressively more;
- each product has separate tolerance;
- unused days recover tolerance;
- tolerance is capped and effects retain a minimum effectiveness.

Heroin liquid remains an `ItemLiquidPortion`. `HeroinVesselDoseSystem` uses the game-bundled Harmony library to intercept the native liquid-container drinking method for this liquid only. Actual removed portions are converted to litres and passed to `AddictionSystem.ApplyHeroinDose`; syringes call the same method with 0.1 L. Visual attributes never infer doses. Other liquids retain native behavior.

Progression timing: the daily withdrawal/decay pass is driven by in-game calendar hours (`Calendar.TotalHours`), not real time; elapsed game hours since the last processed hour are counted on each 1s game-thread tick (catch-up capped at 24h) and the pass runs when `FullHourOfDay == 0`.

Withdrawal applies a movement **slow** via additive walkspeed `-severity * 0.4f` (not a speedup); poison damage above severity 0.8.

## Overdose

`Systems/OverdoseSystem.cs` owns acute load, recovery, risk, movement penalties and poison damage. All custom consumables and heroin consumption call `AddictionSystem.RecordDrugDose` once. Product load values are independent of visual intoxication: opium 3, morphine 6, heroin 5 per 0.1 L, Coca Vitae 5 per item.

Existing `vs-dope-load-<product>` watched keys are retained. `vs-dope-load-gamehour` in persistent entity attributes records the last processed calendar time. Each product loses 5 load units per elapsed in-game hour while online; pausing the calendar prevents metabolism. Joining resets only that timestamp, preserves load and does not create a dose. Death/respawn clears acute load and owned movement effects, preserving addiction/tolerance history.

Risk is the sum of each product's load divided by its tolerance-adjusted threshold (15 to at most 25). Warning begins at risk 0.75, overdose above 1. Severity is `clamp(risk - 1, 0, 1)`. A single combined movement correction caps speed below normal even with stimulant bonuses, with a 0.1 floor to prevent reverse movement. Above severity 0.35, poison damage is `0.2 * severity` per active second. Drug healing is disabled while overdosing. Recovery removes only the overdose correction. See `docs/OVERDOSE.md` for balance and validation.

A 1s `RegisterGameTickListener` replaces the former low-level timer; entity changes run on the game thread and the listener is disposed on shutdown.

## Client UI

`src/Client/OverdoseHudSystem.cs` reads `vs-dope-overdose`, `vs-dope-overdose-risk`, and `vs-dope-overdose-severity` to show high-load, overdose and damage warnings. It is a noninteractive HUD element with localized text.

`src/Client/AddictionCharacterTabSystem.cs` adds the Addiction character tab and reads watched attributes synchronized by `AddictionSystem`.

`src/Client/CocaVitaeEffectHudSystem.cs` displays the Coca Vitae countdown from the watched calendar-hour expiry written by `CocaVitaeItem`.

If the Coca Vitae effect key or expiry attribute changes, update both server/item behavior and HUD.


## Reusable syringes

`Items/SyringeItem.cs` handles the empty/heroin/morphine variants, server-side liquid transfer and held use. Each non-stackable syringe stores integer liquid portions in `ItemStack.Attributes["vs-dope-syringe-portions"]`: 100 portions = 1 litre, 10 portions = one dose. Filled creative/crafted variants default to 100; an empty variant always reads zero. These stack attributes persist through inventory moves, drops and saves. The transient `TempAttributes["vs-dope-syringe-applying"]` flag prevents cancelled/fill interactions from applying doses.

Morphine syringes and oral morphine share `MorphineItem.ApplyDose`, including the overdose healing gate. Heroin syringes explicitly record each dose, including rapid repeats and capped psychedelic values; volume-based vessel consumption shares the same calendar-hour effect key. Syringe health/intoxication/psychedelic additions use the existing heroin liquid's per-litre values multiplied by 0.1; tolerance remains product-specific and overdose combines normalized exposure across products.

`Systems/SyringeRecipeSystem.cs` registers exact vessel-code shapeless filling recipes at AssetsLoaded (order 1.1). Uses vanilla liquid-container recipe attributes to remove 1 litre and preserve the vessel. A client/server recipe-matching guard requires a single vessel to avoid rounding losses from the vanilla stacked-vessel consumption path. Placed refill and off-hand refill use `ILiquidSource`; sealed barrels and claimed blocks are protected.

The project now references `Mods/VSSurvivalMod.dll` for the game's liquid-container interfaces and classes. See `docs/SYRINGES.md` for build and in-game acceptance checks.

## Drug addict NPC

`src/Entities/EntityDrugAddict.cs` (class `vs-dope.drugaddict`, JSON `assets/vs-dope/entities/drugaddict.json`) is an `EntityAgent` with hand-written server steering, not `taskai`. States: `Approach` -> `WaitInteract` -> (`Flee` after a mugging or impatience | `Leave` after a sale or when its target player is gone).

- **Spawning**: `Systems/DrugAddictSpawnSystem.cs` spawns 0-3 per in-game day near a random online player, plus `/spawnaddict [count]` (controlserver). It calls `BindTarget(uid)` before `SpawnEntity`.
- **Trading**: `Systems/AddictTradeSystem.cs` (server) and `Client/AddictTradeUiSystem.cs` share channel `vs-dope.addicttrade`: `SellToAddictPacket` (C->S), `OpenAddictTradePacket` (S->C), `CloseAddictTradePacket` (S->C). All three are registered in that order on both sides. `EntityDrugAddict.AcceptsTrade` gates sales.
- **After a sale**: `OnPurchaseCompleted()` thanks the player and enters `Leave`. The addict lingers `LingerAfterSaleSeconds` (10 s, reset by each sale) so the player can keep selling, then closes the window, picks a heading away from the player and walks off (`LeaveSpeed`). It despawns once it has walked `LeaveMinWalkSeconds` and no online player can see it (nobody within 24 blocks, and nobody within 64 blocks looking within ~60 degrees of it). There is also a `LeaveTimeoutGameHours` (1 in-game hour) fallback. A sale can still roll an overdose (`OverdoseChancePerSale`), which kills it on the spot.
- **Never traded**: this path is unchanged. The addict gives up during approach (>38 blocks), flees after 45 s of waiting (240 s once the window is open) or after a mugging, despawns 30 blocks into a flee, and hits the `MaxLifetimeSeconds` (900 s) cap.
- **Health/death**: server `health` behavior (15 HP) and `deaddecay` (3 in-game hours, corpse then crumbles). The shape carries `hurt` and `die` animations. Attacks run `base.OnInteract` on both sides, which gives the attacking client its hit sound, hurt animation and feedback. `Die` closes any open trade window. Overdose uses `EnumDamageSource.Internal`. Do not use `Void` (5): deaddecay treats it as "despawn immediately".
- **Persistent keys** (WatchedAttributes): `vs-dope-addict-target`, `vs-dope-addict-state`, `vs-dope-addict-engaged`, `vs-dope-addict-friendly`, `vs-dope-addict-leave-heading`, `vs-dope-addict-leave-hours`. A reloaded addict that was walking away keeps walking the same way and has no second linger.
