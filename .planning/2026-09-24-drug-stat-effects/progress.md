# Progress

## 2026-09-24
- Planned with the user and verified the stats by decompiling.
- Implemented DrugStatEffectSystem and wired the dose routes. Build is clean.
- Probe hit an NRE in `GetBehavior` on the bare probe entity. Fixed by overriding the virtual `GetBehavior` in `ProbePlayer`.
- DrugStatsProbe 37/37, OverdoseProbe 75/75, MarijuanaProbe 61/61 on a disposable server.
