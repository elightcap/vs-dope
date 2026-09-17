# Coca Vitae follow-up review (Vintage Story 1.22.x)

This branch follows up on PR #3 and records the issues found after merge.

## Fixed in this branch

### Consumable effect isolation
Previously every drug wrote its movement modifier using the same `vs-dope-drug-speed` stat key. A later dose could overwrite an earlier effect, and an older callback could remove a newer effect.

Each consumable now derives a unique effect key from its item code. Re-dosing the same item refreshes its expiry timestamp, and an older callback will not remove a refreshed effect.

### Clear speed semantics
`SlowFactor` was replaced with `SpeedMultiplier`. This removes the confusing negative slow factor previously used to make Coca Vitae increase movement speed.

- Opium: 0.90x movement
- Morphine: 0.80x movement
- Coca Vitae: 1.15x movement

### Explicit real-time durations
The old `DurationHours * 2500` naming implied in-game hours even though it was milliseconds. The implementation now uses `EffectDurationMs` explicitly.

Coca Vitae is configured for a 30-second movement effect. Existing opium/morphine timings are retained as 2.5/5 seconds for compatibility until their balance is reviewed separately.

## Still required before the coca feature is considered complete

### Renewable harvesting / regrowth
The current crop uses normal block drops at stage 9. Breaking it therefore destroys the crop. The intended design is to harvest mature leaves and return the shrub to an earlier growth stage (target: stage 6) so it can regrow.

This should be implemented as a dedicated crop/block behavior and tested in-game against Vintage Story 1.22.x rather than guessed through JSON.

### Processing integration
There is currently no recipe that turns Coca Leaves into Coca Vitae. The intended processing is a fictional game mechanic using the game's distillation/processing systems. A recipe or ModSystem integration should only be added after confirming the exact 1.22.x API/content schema and validating it in-game.

### Dedicated artwork
Current coca assets intentionally use placeholders:

- plant -> poppy atlas
- coca leaf -> seedpod texture
- coca seeds -> poppy seed texture
- Coca Vitae -> existing powder texture

These should be replaced by dedicated PNG textures. The repository connector used for this review can edit text/source files but cannot create the required PNG binary assets.

### Plant model quality
The nine JSON stages exist, but the geometry is currently low-detail/blocky compared with the existing Papaver assets. A visual pass should replace the large rectangular leaf bars with more natural leaf planes/branches while keeping the nine-stage progression.

## Validation checklist

- Build against the repository's Vintage Story 1.22.x/.NET target.
- Load a test world with only required dependencies and `vs-dope` enabled.
- Plant coca seed and verify stages 1-9 resolve without missing shape/texture errors.
- Verify stage-9 harvest quantity and renewable stage regression once implemented.
- Verify repeated Coca Vitae use refreshes its own effect without cancelling effects from other consumables.
- Verify Coca Vitae heals 4 HP and applies 1.15x movement for 30 seconds.
- Verify multiplayer/server-side consumption removes exactly one item.
- Verify handbook entries do not reference unresolved recipes/assets.

## Notes on Vintage Story 1.22

Vintage Story 1.22 moved public code projects to .NET 10 and expanded tag support for crafting recipes. Any compiled follow-up behavior should therefore be validated using the repository's 1.22.x toolchain rather than older modding examples.
