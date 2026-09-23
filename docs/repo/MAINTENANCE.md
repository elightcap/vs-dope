# Maintenance and Navigation

## Known concerns

1. **README drift:** historical processing/layout details do not exactly match the current tree. Code/assets are authoritative.
2. **Coca processing gap:** Coca Vitae exists, but leaf-to-finished-product processing is not yet implemented with a validated recipe path.
3. **Coca art quality:** coca stage models are simpler than papaver stages and use placeholder/reused textures.
4. **Native drinking hook:** `HeroinVesselDoseSystem` patches the 1.22.7 liquid-container drinking method for heroin only. Re-run `tests/OverdoseProbe` when upgrading the game.
5. **Effect synchronization:** server expiry and client watched attributes must stay consistent.
6. **Build dependency:** local builds require `VINTAGE_STORY` pointing at the target installation.

## Navigation workflow

Before modifying a subsystem:

1. Read `AGENTS.md`, then the relevant file(s) under `docs/repo/`.
2. Fetch the current `master` tree; do not rely on an old map.
3. Inspect relevant JSON and C# together.
4. Search for asset code, class name, stat key, and watched-attribute key before renaming.
5. Create a feature/fix branch from current `master`; changes go through PR review.
6. Document behavioral/balance changes in the PR.
7. Build/test against the target Vintage Story API for API-facing changes.
8. Update the appropriate `docs/repo/` file when architecture changes.

## Search index

- `coca-vitae`: finished coca product / tolerance ID
- `vs-dope-coca-vitae-speed`: Coca movement effect key
- `ToleranceProduct`: per-product tolerance routing
- `RecordToleranceUse`, `GetEffectMultiplier`: tolerance API
- `RecordDrugDose`: shared consumption accounting
- `OverdoseSystem`: combined risk, recovery, damage
- `HeroinVesselDoseSystem`: native volume-based drinking hook
- `crop-coca`, `crop-poppy`: crop codes
- `plantBlockCode`: seed-to-crop linkage
- `shapeByType`: stage shape mapping
- `RegisterItemClass`: JSON custom-class registration
