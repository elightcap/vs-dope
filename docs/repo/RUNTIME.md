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

Heroin is a special case: its JSON is not currently a `DrugConsumableItem`. The system infers use from increases to the player's `psychedelic` watched attribute.

Progression timing: the daily withdrawal/decay pass is driven by in-game calendar hours (`Calendar.TotalHours`), not real time; elapsed game hours since the last processed hour are counted on each 5s timer tick (catch-up capped at 24h) and the pass runs when `FullHourOfDay == 0`.

Withdrawal applies a movement **slow** via additive walkspeed `-severity * 0.4f` (not a speedup); poison damage above severity 0.8.

## Overdose

Per-product concentration overdose, separate from tolerance and from the vanilla `intoxication`/`psychedelic` attributes. Each dose adds raw `IntoxicationAmount` to a watched attribute `vs-dope-load-<product>` (written by `DrugConsumableItem.Consume`; heroin is incremented in the watcher with `HeroinDoseLoad`). Products tracked: opium, morphine, heroin, coca-vitae.

`AddictionSystem.MetabolizeAndCheckOverdose()` runs on every 5s tick (not game-hour gated): it metabolizes each load down by `MetabolismPerTick`, then if a product's load exceeds its threshold applies a hard walkspeed slow and, past `OverdoseDamageSeverityGate`, escalating poison damage. Threshold = `OverdoseBaseThreshold + min(tolerance, TolerancePlateau) * ThresholdPerTolerancePoint` — tolerance raises it only up to the plateau. Near-death but recoverable: no antidote; stopping dosing lets metabolism clear it. Watched bool `vs-dope-overdose` (const `WatchOverdose`) flags active overdose for clients.

## Client UI

`src/Client/AddictionCharacterTabSystem.cs` adds the Addiction character tab and reads watched attributes synchronized by `AddictionSystem`.

`src/Client/CocaVitaeEffectHudSystem.cs` displays the Coca Vitae countdown from the watched calendar-hour expiry written by `CocaVitaeItem`.

If the Coca Vitae effect key or expiry attribute changes, update both server/item behavior and HUD.
