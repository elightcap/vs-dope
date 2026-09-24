# Overdose behavior (Vintage Story 1.22.7)

Every dose rolls a random overdose chance (issue #28). The earlier model added "load" per dose and only overdosed once load crossed a tolerance-scaled threshold. It was deterministic, the load metabolised between spaced-out doses, and a binge raised its own threshold through tolerance, so rapid injections often never tripped it.

## The roll

All consumption routes call `AddictionSystem.RecordDrugDose` once per event on the server: consumable items (opium, morphine, coca vitae), morphine and heroin syringes (0.1 L each) and heroin drunk from vessels (one roll per 0.1 L consumed). The roll uses the product's tolerance from *before* this dose, so a binge does not protect itself.

```
chance = min(0.9, (base + perRecentDose * recentDoses) * (1 - 0.4 * tolerance))
```

`recentDoses` is the number of doses of **any** drug in the last 2 in-game hours (4 real minutes at default calendar speed). Switching drugs does not reset the risk.

| Product | Base chance | + per recent dose | Base severity | P(OD) 1 dose | 3 rapid | 5 rapid | 10 rapid |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Opium | 0.5% | 1.5% | 0.30 | 0.5% | 6% | 16% | 46% |
| Coca Vitae | 1% | 3% | 0.40 | 1% | 12% | 30% | 73% |
| Morphine (item or 0.1 L injection) | 1.5% | 4% | 0.45 | 1.5% | 16% | 39% | 83% |
| Heroin (0.1 L) | 2% | 5% | 0.55 | 2% | 20% | 47% | 90% |

Cumulative probabilities assume a fresh player with no prior tolerance; the tolerance built up by the binge itself is included. Tolerance can cut the chance by at most 30% (tolerance caps at 0.75).

## Outcome

A successful roll adds `baseSeverity + 0.05 * recentDoses` (cap 1) to the current overdose severity. Severity recovers 0.5 per in-game hour.

- Overdosing: red HUD, speed capped at `0.75 - 0.6 * severity` (floor 0.1). Stimulant bonuses cannot cancel it. Drug healing is blocked, including the dose that caused it.
- Severity above 0.25: poison damage `0.5 * severity` per second, applied every second. Totals before severity recovers below the gate (default calendar speed): opium ~1.7 HP, coca vitae ~6 HP, morphine ~8 HP, a single heroin overdose ~14 HP, full severity ~56 HP. A heroin overdose from full health is barely survivable; anything stacked on top is lethal.
- Creative and spectator players take no damage (vanilla `EntityPlayer.ShouldReceiveDamage`). Test overdose damage in survival.
- The yellow HUD warning appears when the next dose of the last-used drug has at least a 15% chance, and shows the percentage.

## Persistence and lifecycle

- Entity `Attributes` (server, saved): `vs-dope-recent-dose-hours` (double array of calendar hours, max 32), `vs-dope-last-dose-product`, `vs-dope-overdose-gamehour` (recovery clock).
- `WatchedAttributes` (synced): `vs-dope-overdose`, `vs-dope-overdose-severity`, `vs-dope-overdose-risk`, `vs-dope-recent-doses`.
- Joining resets only the recovery clock (no offline recovery) and removes the old `vs-dope-load-*` keys.
- Death and respawn clear the overdose, the recent-dose window and this mod's movement effects. Tolerance and addiction stay.

## Screen effects (issue #29)

See `src/Systems/DrugVisualEffects.cs`. Per dose, scaled by the tolerance effect multiplier and capped at the vanilla limits of intoxication 1.1 and psychedelic 2.0. Vanilla detox fades them.

| Product | Intoxication before | Psychedelic before | Intoxication now | Psychedelic now |
| --- | ---: | ---: | ---: | ---: |
| Coca Vitae | 1.0 | 0 | 0.05 | 0.15 |
| Opium | 3.0 | 0 | 0.15 | 0.10 |
| Morphine (item or injection) | 6.0 | 0 | 0.25 | 0.20 |
| Heroin (0.1 L) | 0.1 | 0.15 | 0.35 | 0.40 |

Before, values were clamped at 25 instead of the vanilla 1.1 and 2.0. Item effects were removed again after 2.5 to 5 seconds, so morphine gave a short, violent sway of about five times vanilla's maximum drunkenness.

## Native liquid hook

`HeroinVesselDoseSystem` patches `BlockLiquidContainerBase.tryEatStop` server-side with the game's bundled `Lib/0Harmony.dll`. It replaces only heroin drinking, uses native `SplitStackAndPerformAction`/`TryTakeLiquid`, and reports the exact removed portions. Cancelled uses do nothing.

## Verification

`tests/OverdoseProbe` is a test-only server mod (124 checks, including the practical-effect checks in `ToolEffectChecks.cs`). Build it with `VINTAGE_STORY=/opt/vintagestory dotnet build tests/OverdoseProbe/OverdoseProbe.csproj`. Run it on a disposable server with its own data path, for example `VintagestoryServer --dataPath /tmp/srv --ip 127.0.0.1 --port 42491`, with `Mods/vs-dope/` (modinfo.json, vs-dope.dll, assets/vs-dope) and `Mods/overdoseprobe/` (modinfo.json, OverdoseProbe.dll). Look for `OVERDOSE TEST SUMMARY` or `OVERDOSE TEST FAILED` in `Logs/server-main.log`. It replaces the roll with fixed values, then checks the chance math, every consumption route, the window, recovery, heal blocking, damage, movement bounds, join/respawn and the screen-effect caps. Never ship the probe.
