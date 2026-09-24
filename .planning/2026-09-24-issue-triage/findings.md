# Findings

## Backlog

Open issues: #48 temporal stability, #49 purity, #50 crop genetics/climate,
#51 persistent NPC reputation, #52 runners (depends on #51), #53 staged
withdrawal, #55 practical drug effects. No issue has additional comments or
acceptance decisions at the start of this task. Posted specific findings on all
seven at the user's request. Existing open PR #47 is unrelated and left alone.

## Verified API, Vintage Story 1.22.7

- Runtime is available at `/workspace/scratch/25ddf310c8d1/vs-runtime` and
  .NET 10 at `/workspace/scratch/25ddf310c8d1/dotnet` in this environment.
- `CollectibleObject.GetMiningSpeed` reads `miningSpeedMul` for stone and ore.
- `EntityBehaviorHunger` reads `hungerrate`; `Saturation` is a public property
  whose setter marks the watched hunger tree dirty.
- Legacy `ItemPoultice` reads the intentionally misspelled `healingeffectivness`
  for amount. Live 1.22.7 poultices/bandages instead use `HealingItem`, which reads
  it only for application time. Native tests exposed this difference. The health
  hook scales internal scheduled healing once by our active opiate bonuses;
  immediate healing and delivered ticks are not scaled again.
- `AiTaskBaseTargetable` reads `animalSeekingRange` in player-target checks.
- `EntityBehaviorHealth.OnEntityReceiveDamage` applies damage directly and
  offers no general resistance stat. Filter a server-only hook to physical
  entity attacks; never reduce healing, poison, starvation, or overdose damage.
- `EntityStats.Set` updates/synchronizes the stats tree, with persistent values
  requiring explicit cleanup. Restore effects from expiry/strength records.
- Temporal stability updates directly via `OwnStability` and
  `TempStabChangeVelocity`, not a vanilla stat multiplier in the update path.
- `BlockBed.OnBlockInteractStart` enters sleep via `EntityAgent.TryMount`.

## Scope decisions

Implement #55 first, preserving existing base Coca Vitae/heroin speed contracts.
Opium/morphine tool effects need useful calendar durations; their brief existing
movement penalties can remain unchanged. Marijuana receives tolerance through
the existing per-product tolerance mechanism without introducing overdose rolls.
All additional effects must expire, restore on reconnect, clear on death, avoid
  stacking on repeated doses, and preserve unrelated modifiers.

## Native detection limitation

Both AI generations filter targets using `animalSeekingRange`. Their outer
nearest-entity queries still use configured search radii. Positive modifiers can
offset stealth/light penalties inside that radius; they cannot enlarge it.
Retain these native rules and document this explicitly rather than install
global AI patches. New AI can opt out with `SeekingRangeAffectedByPlayerStat`.
