# Issue #51: Addict memory and reputation

Goal: addicts keep a persistent identity and remember each player. Regulars come back, pay better and share rumours. Violence against addicts spreads (heat). Deaths from your product lower demand. Long-term regulars visibly decline and may recover when cut off.

## Phases
- [x] 1. Research APIs (save data, structures, texture alternates, nametag)
- [x] 2. Ledger data model + pure logic (`src/Systems/AddictLedger.cs`)
- [x] 3. `AddictReputationSystem` (load/save, daily pass, rumours, `/addicts`)
- [x] 4. Entity integration (identity bind, migration, stale duplicates, death/mug/assault hooks, nametag, textureIndex)
- [x] 5. Spawn selection (returning addicts, regular visits, heat/demand multipliers)
- [x] 6. Trade integration (tier price multiplier, visit counting, exposure, window title)
- [x] 7. Decline textures (tools/build_addict_texture.py stages 1-3) + entity JSON alternates + nametag
- [x] 8. Lang keys, probe tests, docs maps
- [x] 9. Build, deploy, logs, commit

Current phase: done (awaiting in-game acceptance)
