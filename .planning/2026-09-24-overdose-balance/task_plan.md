# Task Plan: Per-dose overdose roll (#28) and drug strength balance (#29)

## Goal

- #28: every dose rolls a random overdose chance (per-drug base + increase per recent dose in a rolling game-hour window, reduced by tolerance). All consumption paths use it. Keep the existing overdose outcome.
- #29: reduce morphine's visual effect and put every drug on a clear, tiered, vanilla-ranged scale.

## Current Phase

Complete

## Phases

### Phase 1: Diagnose
- [x] Find why 10 rapid shots with no tolerance did not OD (see findings.md)
- [x] Find where visual strength is set for each drug
- **Status:** complete

### Phase 2: Implement overdose roll
- [x] Rewrite OverdoseSystem around dose timestamps + roll + severity recovery
- [x] Wire all consumption paths through AddictionSystem.RecordDrugDose
- [x] HUD shows next-dose risk %
- **Status:** complete

### Phase 3: Rebalance visuals
- [x] DrugVisualEffects per-drug constants, vanilla caps, tolerance scaling, legacy clamp on join
- **Status:** complete

### Phase 4: Verify and document
- [x] dotnet build 0 errors, no new warnings; OverdoseProbe 75/75 on a disposable 1.22.7 server
- [x] docs/repo maps + docs/OVERDOSE.md updated
- [x] Commit (no push, no deploy)
- **Status:** complete

## Next Step

User playtest in survival (see progress.md). Coordinator deploys.
