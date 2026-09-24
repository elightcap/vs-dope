# Progress Log

## Session: 2026-09-24

- Read CLAUDE.md, docs/repo/README.md, RUNTIME.md, 2026-09-18 overdose planning notes, all overdose-related source.
- Diagnosed #28 from archived logs: 10 heroin heals and no overdose. The deployed DLL came from the addict branch, which predates the #26 overdose fix (old real-time metabolism outpaced the doses), and the first 6 shots were taken in Creative, where vanilla blocks poison damage. Details are in findings.md.
- Decompiled VintagestoryAPI, VSEssentials, VSSurvivalMod and VintagestoryLib to scratchpad. Found the vanilla intoxication/psychedelic caps (1.1 / 2.0), the detox rate, and the perception effect math.
- Rewrote `src/Systems/OverdoseSystem.cs` (per-dose roll, recent-dose window, severity recovery). Added `src/Systems/DrugVisualEffects.cs`. Updated AddictionSystem, DrugConsumableItem, OverdoseHudSystem, lang, and heroin.json.
- Build: 0 errors, 62 warnings both before and after the change (none in touched files).
- Rewrote tests/OverdoseProbe. The first run failed on "opium HUD risk reaches warning level": binge tolerance keeps opium at 13% after 12 doses. That was a wrong expectation, so the check now asserts risk growth against ChanceFor. The second run passed 75/75 on a disposable server (`--dataPath` in scratchpad, port 42491). Nothing was deployed to the shared Mods folder.
- Updated docs/repo RUNTIME, DEPENDENCIES, MAINTENANCE, STRUCTURE and docs/OVERDOSE.md.
