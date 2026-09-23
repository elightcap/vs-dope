# Overdose behavior (Vintage Story 1.22.7)

The old implementation inferred vessel doses from increases in `psychedelic`, used visual intoxication as dose strength, removed load every five real seconds, and overwrote the movement penalty when several products were active. This missed capped/rapid vessel uses and made Coca Vitae's low load difficult to accumulate. Drug healing could also mask overdose damage.

## Consumption and balance

All supported routes record one consumption event immediately on the server. Liquid heroin exposure uses the actual litres removed, independent of vessel size and visual-effect caps. Syringes retain their 1 L capacity and ten 0.1 L applications.

| Product / route | Added load |
| --- | ---: |
| Opium item | 3 |
| Morphine item or 0.1 L morphine injection | 6 |
| Heroin injection or vessel consumption | 5 per 0.1 L consumed |
| Coca Vitae item | 5 |

Each product retains its own tolerance and existing saved load key. Its threshold is `15 + min(tolerance, 0.5) * 20`, capped at 25. Overall risk is the **sum** of the product load/threshold ratios, so switching drugs cannot evade overdose.

- Risk at least 0.75: yellow high-load warning.
- Risk above 1: red overdose warning and reduced movement.
- Severity is `clamp(risk - 1, 0, 1)`. Above 0.35 severity, poison damage is `0.2 * severity` health per active second, evaluated on the game thread.
- The movement correction caps total speed at `0.75 - 0.6 * severity`, with a 0.1 floor. It accounts for active drug/stat bonuses and never produces reverse movement.
- These drug items no longer heal while the player is overdosing, including the dose which crosses the threshold.
- Each product clears 5 load units per elapsed in-game hour while online. Recovery removes the overdose penalty without removing another active effect. Severe overdose can be fatal; survival depends on accumulated load and remaining health.

As a quick check with a fresh player and negligible elapsed game time, four heroin injections or four Coca Vitae items trigger overdose even after their new tolerance is included. Eight heroin injections cross the damage gate. Timing and prior tolerance alter the exact count.

Heroin's base -50% and Coca Vitae's base +100% movement effects still last one in-game hour, refreshed by another use, with tolerance scaling strength. Their normal effects remain separate from overdose penalties.

## Persistence and lifecycle

- Existing watched `vs-dope-load-{opium,morphine,heroin,coca-vitae}` values remain compatible.
- Watched `vs-dope-overdose` remains the active flag. New watched `vs-dope-overdose-risk` and `vs-dope-overdose-severity` drive the HUD.
- Persistent entity attribute `vs-dope-load-gamehour` is the last metabolism timestamp.
- Joining resumes saved load, resets that timestamp and creates no artificial dose. Offline time grants no recovery.
- Death and respawn clear acute load and this mod's drug movement effects. Addiction and tolerance history remain.
- Paused game time does not clear load. Metabolism depends on elapsed calendar time, not callback count. Damage uses active callback time with a five-second catch-up cap.

## Native liquid hook

`HeroinVesselDoseSystem` patches `BlockLiquidContainerBase.tryEatStop` server-side with the game's bundled `Lib/0Harmony.dll`. It replaces only heroin drinking, uses native `SplitStackAndPerformAction`/`TryTakeLiquid` for vessel handling, and reports the exact removed portions. Cancelled uses do nothing. Other liquids execute the original method. Morphine solution remains a syringe source, not a newly added drink.

The patch is removed on mod disposal. Verify this hook when upgrading Vintage Story or using another mod that replaces native liquid drinking.

## Verification

Verified on Vintage Story 1.22.7: mod builds with zero errors; the headless server probe passes 54 assertions. Four existing nullable warnings remain in the older Coca Vitae HUD and Addiction tab.

Build the mod and separate probe with a 1.22.7 installation:

```bash
VINTAGE_STORY=/path/to/vintagestory dotnet build
VINTAGE_STORY=/path/to/vintagestory dotnet build tests/OverdoseProbe
```

Install the mod normally on a disposable server. Add a separate test-mod folder containing `tests/OverdoseProbe/modinfo.json` and `tests/OverdoseProbe/bin/Debug/net10.0/OverdoseProbe.dll`. The probe runs at GameReady and logs `OVERDOSE TEST PASS`, `OVERDOSE TEST FAILED`, and a summary. Do not ship the probe on a production server.

The probe uses actual loaded items, native container operations, the installed Harmony hook, server dose methods, and syringe interaction handlers. Player/network endpoints are stand-ins; damage is captured at `ReceiveDamage`. It checks repeated doses for every product, mixed exposure, stimulant interaction, movement bounds, suppressed healing, game-clock recovery, callback-frequency independence, relog state, death/respawn cleanup, capped psychedelic values, exact and partial vessel consumption, stacked vessels, and ten syringe applications.

Client acceptance still requires an actual game session: confirm the three HUD stages render without capturing input; check health decreases and movement slows in survival mode; confirm warnings clear on recovery/death and verify relog/restart persistence with a saved player. Repeat with a connected multiplayer client.
