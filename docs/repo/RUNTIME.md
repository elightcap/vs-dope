# Runtime Architecture

## Mod entry point

`src/VsDopeModSystem.cs` registers custom item classes used by JSON, creates `AddictionSystem` server-side, and exposes the static system reference used by consumables.

If a JSON `class` changes or a new custom item class is introduced, inspect registration here.

## Consumables

`src/Items/DrugConsumableItem.cs` is the primary path for directly consumed custom items. It owns held interaction, consumption, healing, movement modifiers, expiration, addiction recording, and product-specific tolerance. Screen effects come from `Systems/DrugVisualEffects.cs`.

Current subclasses:
- `OpiumItem`
- `MorphineItem`
- `CocaVitaeItem`

Tolerance is keyed by `ToleranceProduct`. Keep product IDs stable unless intentionally resetting/migrating stored player tolerance.

Coca Vitae writes its effect expiry to watched attributes for its client HUD. Its movement boost expires by comparing the calendar's total hours, so it lasts exactly one in-game hour even if the calendar speed changes.

Heroin writes `vs-dope-heroin-slow-expires-gamehour` on consumption. The addiction system removes its movement penalty after one in-game calendar hour rather than when the psychedelic attribute ends.

## Practical drug effects

`Systems/DrugToolEffects.cs` adds calendar-timed mining/hunger benefits and a coca crash, and opium/morphine bandage healing, accuracy, detection and physical-attack trade-offs. `DrugConsumableItem.ApplyDose` passes its pre-dose tolerance strength, including the morphine-syringe path. Named constants define balance. The server system restores saved strength/expiry on reconnect and clears only its own stat sources on death/expiry. Coca's one-time crash cost floors satiety at zero; its half-hour movement penalty cannot be erased by another dose.

Persistent watched keys: `vs-dope-tool-{coca-vitae,opium,morphine}-expires-gamehour` and `-strength`; `vs-dope-coca-crash-expires-gamehour` and `-strength`. Stat sources are `vs-dope-tool-<product>` and `vs-dope-coca-crash`. The Coca HUD shows the crash countdown as well as the established active timer.

The server-only health Harmony hook reduces physical Entity/Player attacks; strongest active opiate protection wins. It does not reduce overdose poison or environmental damage. It also increases the amount of internal scheduled healing (live 1.22.7 bandages/poultices) once, without multiplying delivered ticks again. `healingeffectivness` affects their application time and legacy poultice amount. Vanilla `animalSeekingRange` still respects AI configuration and outer search radii. Balance, lifecycle, exact native stat semantics, regression tests and client acceptance are documented in `docs/DRUG_TOOLS.md`.

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

## Screen effects

`Systems/DrugVisualEffects.cs` holds the per-dose `intoxication` (drunk sway) and `psychedelic` (colour warp) added by each product: coca vitae 0.05/0.15, opium 0.15/0.10, morphine 0.25/0.20, heroin 0.35/0.40 per 0.1 L. Amounts scale with the tolerance effect multiplier and are capped at the vanilla limits (1.1 and 2.0). They are not removed when the movement effect expires; vanilla `EntityBehaviorHunger.detox` fades both (about 0.6 per in-game hour). On join, values saved above the caps by older builds are clamped.

## Overdose

`Systems/OverdoseSystem.cs` owns the per-dose overdose roll, overdose severity, recovery, movement penalty and poison damage. Every consumption route (items, morphine/heroin syringes, heroin vessels) goes through `AddictionSystem.RecordDrugDose`, which rolls against the tolerance the player had before this dose and then records addiction and tolerance.

Chance per dose = `(base + perRecentDose * recentDoses) * (1 - 0.4 * tolerance)`, capped at 0.9. `recentDoses` counts doses of any drug in the last 2 in-game hours (persistent `vs-dope-recent-dose-hours` double array in entity `Attributes`, max 32 entries). Per-drug `Risks` table (base / per recent dose / base severity): opium 0.5% / 1.5% / 0.30, coca vitae 1% / 3% / 0.40, morphine 1.5% / 4% / 0.45, heroin 2% / 5% / 0.55. A vessel drink rolls once per 0.1 L consumed.

A successful roll adds `baseSeverity + 0.05 * recentDoses` (cap 1) to the current severity and sets `vs-dope-overdose`. Severity recovers 0.5 per elapsed in-game hour while online (`vs-dope-overdose-gamehour` clock; paused calendar and offline time do not recover). While overdosing: speed is capped at `0.75 - 0.6 * severity` (floor 0.1, stimulants cannot cancel it), drug healing is blocked, and above severity 0.25 the player takes `0.5 * severity` poison damage per second (applied on the 1 s addiction tick). Creative/spectator players ignore that damage (vanilla `EntityPlayer.ShouldReceiveDamage`).

Watched keys: `vs-dope-overdose` (bool), `vs-dope-overdose-severity`, `vs-dope-overdose-risk` (chance of the next dose of the last-used product), `vs-dope-recent-doses`. Legacy `vs-dope-load-<product>` / `vs-dope-load-gamehour` keys are removed on join. Death/respawn clears overdose state and recent doses and keeps tolerance/addiction. A 1s `RegisterGameTickListener` drives `OverdoseSystem.Tick`. See `docs/OVERDOSE.md` and `tests/OverdoseProbe`.

## Client UI

`src/Client/OverdoseHudSystem.cs` reads `vs-dope-overdose`, `vs-dope-overdose-risk`, and `vs-dope-overdose-severity` to show a next-dose risk warning (from 15%, with the percentage), overdose and damage warnings. It is a noninteractive HUD element with localized text.

`src/Client/AddictionCharacterTabSystem.cs` adds the Addiction character tab and reads watched attributes synchronized by `AddictionSystem`.

`src/Client/CocaVitaeEffectHudSystem.cs` displays the Coca Vitae countdown from the watched calendar-hour expiry written by `CocaVitaeItem`.

If the Coca Vitae effect key or expiry attribute changes, update both server/item behavior and HUD.


## Reusable syringes

`Items/SyringeItem.cs` handles the empty/heroin/morphine variants, server-side liquid transfer and held use. Each non-stackable syringe stores integer liquid portions in `ItemStack.Attributes["vs-dope-syringe-portions"]`: 100 portions = 1 litre, 10 portions = one dose. Filled creative/crafted variants default to 100; an empty variant always reads zero. These stack attributes persist through inventory moves, drops and saves. The transient `TempAttributes["vs-dope-syringe-applying"]` flag prevents cancelled/fill interactions from applying doses.

Morphine syringes and oral morphine share `MorphineItem.ApplyDose`, including the overdose healing gate. Heroin syringes explicitly record each dose, including rapid repeats and capped psychedelic values; volume-based vessel consumption shares the same calendar-hour effect key. A heroin injection is 0.1 L: 2.4 health and the heroin row of `DrugVisualEffects`; a morphine injection equals one morphine item. Tolerance remains product-specific; overdose risk stacks across products through the shared recent-dose window.

`Systems/SyringeRecipeSystem.cs` registers exact vessel-code shapeless filling recipes at AssetsLoaded (order 1.1). Uses vanilla liquid-container recipe attributes to remove 1 litre and preserve the vessel. A client/server recipe-matching guard requires a single vessel to avoid rounding losses from the vanilla stacked-vessel consumption path. Placed refill and off-hand refill use `ILiquidSource`; sealed barrels and claimed blocks are protected.

The project now references `Mods/VSSurvivalMod.dll` for the game's liquid-container interfaces and classes. See `docs/SYRINGES.md` for build and in-game acceptance checks.

## Drug addict NPC

`src/Entities/EntityDrugAddict.cs` (class `vs-dope.drugaddict`, JSON `assets/vs-dope/entities/drugaddict.json`) is an `EntityAgent` with hand-written server steering, not `taskai`. States: `Approach` -> `WaitInteract` -> (`Flee` after a mugging or impatience | `Leave` after a sale or when its target player is gone).

- **Spawning**: `Systems/DrugAddictSpawnSystem.cs` spawns 0-3 per in-game day near a random online player, plus `/spawnaddict [count]` (controlserver, registered through `api.ChatCommands`, count 1-20). It calls `BindTarget(uid)` before `SpawnEntity`.
- **Movement speed**: `WalkSpeed`/`FleeSpeed`/`LeaveSpeed` are *net* WalkVector magnitudes. Server entities move about `135 * w` blocks/s (1/30 s physics tick, `PModuleOnGround` drag 0.3); a player walks ~3.4 b/s and sprints ~6.8 b/s. Approach 0.025 (~3.4 b/s), flee 0.034 (~4.6 b/s, a sprinting player catches it), leave 0.018 (~2.4 b/s). Fleeing sets `Controls.Sprint` for the run animation; sprint doubles ground speed for every `EntityAgent`, so `Walk()` divides it back out. Derivation in `.planning/2026-09-24-addict-inventory/findings.md`.
- **Pockets (inventory)**: `Entities/AddictPockets.cs`, owned by the entity via `EntityDrugAddict.Pockets` (server only). A plain `ItemStack` list persisted in the server-only `Entity.Attributes` tree `vs-dope-addict-inv` (`count`, `s0..sN`). It loads lazily on first server access (first tick, trade or death), which is always after `FromBytes`. A missing key rolls the starting stock: 6-45 `game:gear-rusty`, 2-4 kinds of scavenged vanilla junk (each with a gear-equivalent value, `rot` is worth 0) and a 30% chance of a small drug. Call `SavePockets()` after every change. A mugging puts the stolen hand stack into the mugger's pockets.
- **Trading**: `Systems/AddictTradeSystem.cs` (server) and `Client/AddictTradeUiSystem.cs` share channel `vs-dope.addicttrade`: `SellToAddictPacket` (C->S), `OpenAddictTradePacket` (S->C), `CloseAddictTradePacket` (S->C). All three are registered in that order on both sides. `EntityDrugAddict.AcceptsTrade` gates sales. The addict pays from its pockets: affordable units = `(gears + goods value) / unit price`. If that is 0 the sale is refused (`addict-cant-afford`). If it is below the asked quantity, the addict buys what it can. It pays gears first and the rest in junk (most valuable item that doesn't overshoot, else the cheapest that covers it, with no change given). Starting or bought drugs are never payment. Payment that doesn't fit the player's inventory drops at their feet. Bought solid drugs merge into the pockets. Bought liquids (heroin) are poured into `game:jug-blue-fired` (3 L), topping up existing jugs first.
- **Trade window**: `OpenAddictTradePacket` carries the offers plus `PlayerHeld[]`, `PlayerGears`, `AddictGears`, `AddictGoodsValue`, `AddictStacks` (nested `AddictStackData { byte[] Stack }` from `ItemStack.ToBytes()`) and `Refresh`. The server re-sends it with `Refresh = true` after every sale (and after a refused sale). The client applies a refresh only to a window that is already open for that addict. Layout follows vanilla `GuiDialogTrader`: pockets grid with the gears/goods line, a buy list (slot, name, "you have N", price, Sell 1/Sell all), and a footer with the player's gear count and Goodbye.
- **After a sale**: `OnPurchaseCompleted()` thanks the player and enters `Leave`. The addict lingers `LingerAfterSaleSeconds` (10 s, reset by each sale) so the player can keep selling, then closes the window, picks a heading away from the player and walks off (`LeaveSpeed`). It despawns once it has walked `LeaveMinWalkSeconds` and no online player can see it (nobody within 24 blocks, and nobody within 64 blocks looking within ~60 degrees of it). There is also a `LeaveTimeoutGameHours` (1 in-game hour) fallback. A sale can still roll an overdose (`OverdoseChancePerSale`), which kills it on the spot.
- **Never traded**: this path is unchanged. The addict gives up during approach (>38 blocks), flees after 45 s of waiting (240 s once the window is open) or after a mugging, despawns 30 blocks into a flee, and hits the `MaxLifetimeSeconds` (900 s) cap.
- **Health/death**: server `health` behavior (15 HP) and `deaddecay` (3 in-game hours, corpse then crumbles). The shape carries `hurt` and `die` animations. Attacks run `base.OnInteract` on both sides, which gives the attacking client its hit sound, hurt animation and feedback. `Die` closes any open trade window. `GetDrops` (called by `Entity.Die` only for `EnumDespawnReason.Death`, so both kills and `Overdose()`) appends and empties the pockets. `DespawnSelf` uses `Removed` and drops nothing. Overdose uses `EnumDamageSource.Internal`. Do not use `Void` (5): deaddecay treats it as "despawn immediately".
- **Persistent keys** (Attributes, server only): `vs-dope-addict-inv` (pockets). (WatchedAttributes): `vs-dope-addict-target`, `vs-dope-addict-state`, `vs-dope-addict-engaged`, `vs-dope-addict-friendly`, `vs-dope-addict-leave-heading`, `vs-dope-addict-leave-hours`. A reloaded addict that was walking away keeps walking the same way and has no second linger.

## Marijuana / Stoned

`JointItem` is registered as `vs-dope.joint`. Five-second right-click held use runs `vsdope-smoke` (150 frames, first-/third-person variants), broadcasts drag audio after 0.9s, then server-side consumes one joint and calls `StonedSystem.Apply`. Temporary stack flags prevent short, cancelled or duplicate-stop consumption.

`BongItem` (`vs-dope.bong`) provides non-stackable `bong-empty`/`bong-loaded` variants. Loading is a grid recipe. Five-second use shares the tested smoking animations and `StonedSystem`, but broadcasts `bong-bubbles` and replaces the loaded item with an empty bong in the same slot. Only completed server-side use performs the replacement; full inventory, cancellation, death and duplicate callbacks are covered by `tests/MarijuanaProbe/BongChecks.cs`. See `docs/BONG.md`.

`StonedSystem` is an auto-loaded ModSystem with a 250ms server tick for fully playing players. Watched `vs-dope-stoned-expires-gamehour` persists a two-hour calendar expiry; entity `Attributes` key `vs-dope-stoned-last-gamehour` tracks the last healing interval. Baseline effects under `vs-dope-stoned` are additive walkspeed -0.2, hunger +0.25, detection factor -0.25, and 0.5 HP/game minute including fractional intervals and final expiry. It refreshes without stacking and clears all its sources on death/respawn. Watched `vs-dope-stoned-strength` stores the marijuana-specific pre-dose tolerance multiplier (missing legacy values default to 1); speed, healing, hunger and detection magnitudes all scale. `RecordToleranceUse` records marijuana without opiate addiction/overdose. `PlayerNowPlaying` resets the healing cursor, preventing offline grants. The HUD displays actual movement/healing strength.

`StonedHudSystem` reads expiry and displays a noninteractive localized HUD. `StonedEyesBehavior` is appended after the vanilla client inventory/skin behaviors; it hooks `EntityBehaviorTexturedClothing.OnReloadSkin`, overlays vanilla sclera and requests recomposition on expiry changes, without altering saved customization. `VSEssentials.dll` is required for that public behavior API. Custom player atlases are skipped. See `docs/MARIJUANA.md` and `tests/MarijuanaProbe`.
