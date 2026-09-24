# Drug effects as tools (issue #55)

Goal: give each drug a practical use through vanilla player stats, each with a trade-off, scaled by tolerance.

- [x] Research stat codes and hooks against the 1.22.7 DLLs
- [x] DrugStatEffectSystem (profiles, apply/tick/resume/clear, coca crash, opioid mitigation)
- [x] Wire dose routes: DrugConsumableItem, ApplyHeroinDose, StonedSystem
- [x] Tooltips + lang keys
- [x] tests/DrugStatsProbe (37 checks), plus Overdose (75) and Marijuana (61) probes still pass
- [x] Docs: RUNTIME.md, DEPENDENCIES.md, CLAUDE.md §4
- [x] Deploy + log check
- [ ] In-game playtest by the user

Current phase: waiting for the playtest. Next step: tune the numbers from playtest feedback.
