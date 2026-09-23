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

Heroin writes `vs-dope-heroin-slow-expires-gamehour` when detected. The addiction system removes its movement penalty after one in-game calendar hour rather than when the psychedelic attribute ends.

## Addiction and tolerance

`src/Systems/AddictionSystem.cs` owns addiction level, consecutive-use tracking, withdrawal, decay, product-specific tolerance, tolerance recovery, and the heroin watcher.

Tolerance behavior:
- first 2 uses of a product per in-game day do not add tolerance;
- heavier same-day use adds progressively more;
- each product has separate tolerance;
- unused days recover tolerance;
- tolerance is capped and effects retain a minimum effectiveness.

Heroin liquid remains an `ItemLiquidPortion`. Vessel use is inferred from increases to the player's `psychedelic` attribute. Syringe use calls `AddictionSystem.ApplyHeroinSyringeDose` directly, updating the watcher baseline to avoid recording the injection twice. Both routes share `RecordHeroinDose`.

Progression timing: the daily withdrawal/decay pass is driven by in-game calendar hours (`Calendar.TotalHours`), not real time; elapsed game hours since the last processed hour are counted on each 5s timer tick (catch-up capped at 24h) and the pass runs when `FullHourOfDay == 0`.

Withdrawal applies a movement **slow** via additive walkspeed `-severity * 0.4f` (not a speedup); poison damage above severity 0.8.

## Overdose

Per-product concentration overdose, separate from tolerance and from the vanilla `intoxication`/`psychedelic` attributes. Each dose adds raw `IntoxicationAmount` to a watched attribute `vs-dope-load-<product>` (written by `DrugConsumableItem.Consume`; heroin is incremented in the watcher with `HeroinDoseLoad`). Products tracked: opium, morphine, heroin, coca-vitae.

`AddictionSystem.MetabolizeAndCheckOverdose()` runs on every 5s tick (not game-hour gated): it metabolizes each load down by `MetabolismPerTick`, then if a product's load exceeds its threshold applies a hard walkspeed slow and, past `OverdoseDamageSeverityGate`, escalating poison damage. Threshold = `OverdoseBaseThreshold + min(tolerance, TolerancePlateau) * ThresholdPerTolerancePoint` — tolerance raises it only up to the plateau. Near-death but recoverable: no antidote; stopping dosing lets metabolism clear it. Watched bool `vs-dope-overdose` (const `WatchOverdose`) flags active overdose for clients.

## Client UI

`src/Client/AddictionCharacterTabSystem.cs` adds the Addiction character tab and reads watched attributes synchronized by `AddictionSystem`.

`src/Client/CocaVitaeEffectHudSystem.cs` displays the Coca Vitae countdown from the watched calendar-hour expiry written by `CocaVitaeItem`.

If the Coca Vitae effect key or expiry attribute changes, update both server/item behavior and HUD.


## Reusable syringes

`Items/SyringeItem.cs` handles the empty/heroin/morphine variants, server-side liquid transfer and held use. Each non-stackable syringe stores integer liquid portions in `ItemStack.Attributes["vs-dope-syringe-portions"]`: 100 portions = 1 litre, 10 portions = one dose. Filled creative/crafted variants default to 100; an empty variant always reads zero. These stack attributes persist through inventory moves, drops and saves. The transient `TempAttributes["vs-dope-syringe-applying"]` flag prevents cancelled/fill interactions from applying doses.

Morphine syringes call the existing `MorphineItem.ApplyDose`, extracted from `DrugConsumableItem.Consume` without changing oral item effects. Heroin syringes explicitly record each dose, including rapid repeats and capped psychedelic values; the calendar-hour effect key is shared with vessel consumption. Syringe health/intoxication/psychedelic additions use the existing heroin liquid's per-litre values multiplied by 0.1; tolerance and overdose remain product-specific.

`Systems/SyringeRecipeSystem.cs` registers exact vessel-code shapeless filling recipes at AssetsLoaded (order 1.1). Uses vanilla liquid-container recipe attributes to remove 1 litre and preserve the vessel. A client/server recipe-matching guard requires a single vessel to avoid rounding losses from the vanilla stacked-vessel consumption path. Placed refill and off-hand refill use `ILiquidSource`; sealed barrels and claimed blocks are protected.

The project now references `Mods/VSSurvivalMod.dll` for the game's liquid-container interfaces and classes. See `docs/SYRINGES.md` for build and in-game acceptance checks.
