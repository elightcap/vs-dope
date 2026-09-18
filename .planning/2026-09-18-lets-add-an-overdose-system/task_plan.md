# Task Plan: Add an Overdose System

## Goal

Add an overdose mechanic so that accumulating too much drug intoxication (relative to a player's tolerance and addiction) triggers escalating harmful effects — from sickness to potential death — integrated with the existing consumption, tolerance, and addiction systems.

## Next Step

All phases complete. Optional follow-up (user): in-game playtest to tune `OverdoseBaseThreshold`, `MetabolismPerTick`, and damage rate for desired lethality feel, then deploy per AGENTS.md (`cp` build output to Mods dir).

## Current Phase

Complete — all 5 phases done

## Phases

### Phase 1: Requirements & Discovery

- [x] Map existing consumption → intoxication/tolerance/addiction flow (DrugConsumableItem, AddictionSystem)
- [x] Identify where an overdose check should hook (immediate on consume vs. ticked)
- [x] Define trigger semantics: threshold source, scaling with tolerance/addiction, per-product vs. total
- [x] Document findings in findings.md
- **Status:** complete

### Phase 2: Design & Structure

- [x] Decide overdose model (per-product load; tolerance-scaled threshold with plateau; near-death DoT + slow)
- [x] Decide persistence keys and watched attributes (`vs-dope-load-<product>`, `vs-dope-overdose`)
- [x] Decide recovery/clearing path (natural metabolism per tick; no antidote)
- [x] Document decisions with rationale in findings.md
- **Status:** complete

### Phase 3: Implementation

- [x] Implement overdose detection + effect application (`MetabolizeAndCheckOverdose`)
- [x] Wire into consumption (`DrugConsumableItem.Consume` load accumulation) and `AddictionSystem.OnTick`; heroin via watcher
- [x] Status effect assets — none needed (uses Stats/damage + watched bool, no new JSON)
- [x] Build cleanly (`dotnet build`) — 0 errors
- **Status:** complete

### Phase 4: Testing & Verification

- [x] Verify thresholds trigger correctly against clamp ranges (logic trace vs. per-product dose sizes; see progress.md)
- [x] Document test results / in-game verification steps in progress.md
- [ ] Fix issues found — none from build/logic trace; full in-game playtest pending user
- **Status:** complete

### Phase 5: Delivery

- [x] Review all changed files (`git diff`): DrugConsumableItem.cs, AddictionSystem.cs
- [x] Update relevant `docs/repo/` maps (RUNTIME, DEPENDENCIES) per AGENTS.md convention
- [x] Summarize deliverable to user
- **Status:** complete

## Key Questions

1. Trigger on total intoxication or per-product concentration? → **Per-product** (new `vs-dope-load-<product>` attrs).
2. Lethality intent? → **Near-death, recoverable** (escalating DoT + heavy slow; survivable if you stop dosing/rest).
3. Does tolerance raise/lower threshold? → **Raises but plateaus**: `15 + min(tolerance,0.5)*20`, flat beyond tolerance 0.5.
4. Antidote or metabolism only? → **Natural metabolism only** (1.5 load/tick; no antidote).

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Separate per-product load attr vs shared intoxication stat | User wanted per-product concentration; keeps binge state independent of the 0–25 display stat and its existing item-expiry handling. |
| Tolerance-scaled threshold with a plateau (`min(tol,0.5)`) | User: "threshold remains the same even as tolerance goes up" past a point. |
| Near-death DoT gated at severity > 0.35 + additive slow cap -0.85 | Recoverable near-death feel; mild OD is debuff-only, severe can kill if ignored but clears fast once dosing stops. |
| Natural metabolism in the existing 5s `OnTick` watcher loop | Mirrors `WatchHeroinEffects`; no new timer or client system needed. |

## Errors Encountered

| Error | Attempt | Resolution |
|-------|---------|------------|
|       | 1       |            |

## Notes

- Reuse existing additive `entity.Stats` / watched-attribute patterns (see AddictionSystem heroin slow at line ~146).
- Intoxication is currently clamped 0–25 in DrugConsumableItem; overdose thresholds must be defined within/against that range.
- Follow AGENTS.md: register classes via ModSystem, keep modid `vs-dope`, update docs/repo maps on new subsystems/persistent keys.
