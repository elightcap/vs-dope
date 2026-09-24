# Cross-file Dependencies

## Coca Vitae

```text
assets/vs-dope/itemtypes/coca-vitae.json
        │ class
        ▼
src/VsDopeModSystem.cs
        │ registers
        ▼
src/Items/DrugConsumableItem.cs :: CocaVitaeItem
        ├── AddictionSystem.GetEffectMultiplier()
        ├── AddictionSystem.RecordUse()
        ├── AddictionSystem.RecordToleranceUse()
        └── watched expiry
                │
                ▼
src/Client/CocaVitaeEffectHudSystem.cs
```

## Addiction/tolerance

```text
DrugConsumableItem subclasses ──┐
                                ├──> AddictionSystem
Heroin vessel/syringe dose ─────┘
                                      │
                                      └── watched attributes
                                               │
                                               ▼
                                   AddictionCharacterTabSystem
```

## Overdose

`DrugConsumableItem.ApplyDose` and `AddictionSystem.ApplyHeroinDose` -> `RecordDrugDose` -> `OverdoseSystem.RecordDose`. `HeroinVesselDoseSystem` hooks native `BlockLiquidContainerBase.tryEatStop` with game-bundled Harmony and reports exact consumed volume. It delegates all other liquids to the original method.

`OverdoseSystem.Tick` -> calendar-based load decay -> combined normalized risk -> one movement correction and poison damage -> watched risk/severity/active keys -> `Client/OverdoseHudSystem`.

Products: opium, morphine, heroin, coca-vitae. Adding a product requires its explicit load and inclusion in `OverdoseSystem.Products`. See `docs/OVERDOSE.md` for persistence and balance.

## Crops

```text
seed item -> plantBlockCode -> crop blocktype
                               ├── shapeByType -> stage shape JSON
                               ├── textures -> texture assets
                               └── drops -> harvested item JSON
```

## Common changes

| Goal | Primary file(s) | Also inspect |
| --- | --- | --- |
| Coca Vitae stats | `DrugConsumableItem.cs` | Coca HUD, tolerance |
| Tolerance balance | `AddictionSystem.cs` | all finished products |
| Overdose threshold/balance | `OverdoseSystem.cs` | `RecordDrugDose`, HUD stages |
| New overdose-able product | `OverdoseSystem.cs` | explicit dose load, tolerance key |
| New consumed product | item JSON + consumable C# | mod registration, lang, texture |
| New liquid / creative filled vessel | liquid itemtype `creativeinventoryStacks` (`game:woodbucket`, `game:barrel`, `ucontents` with `vs-dope:` code) | `waterTightContainerProps`, shape `game:item/liquid`, lang |
| New crop | blocktype + seed item | shapes, textures, lang, drops |
| Crop appearance | `shapes/plant/` | blocktype texture aliases |
| Crop drops | crop blocktype | item definitions |
| Addiction UI | `AddictionCharacterTabSystem.cs` | watched attribute sync |
| Coca countdown | `CocaVitaeEffectHudSystem.cs` | Coca expiry writes |
| Overdose threshold/severity | `OverdoseSystem.cs` (`Refresh`) | per-product load attrs, tolerance plateau |
| Overdose dose accumulation | `RecordDrugDose`, `HeroinVesselDoseSystem` | consumed volume, product IDs in `OverdoseSystem` |
| Processing | `assets/vs-dope/recipes/` | input/output item JSON |
| Localization | `assets/vs-dope/lang/en.json` | exact asset codes |

## Syringes

`itemtypes/syringe.json` -> `VsDopeModSystem` registration -> `Items/SyringeItem.cs` -> `MorphineItem.ApplyDose` / `AddictionSystem.ApplyHeroinSyringeDose`. `SyringeRecipeSystem` discovers liquid-container blocks and registers filling recipes; empty crafting is JSON. Content variants map to the three syringe texture PNGs and English localization. Liquid volume relies on the current heroin and morphine-solution `itemsPerLitre: 100` contract; unsupported densities are rejected. Native container types require the VSSurvivalMod assembly reference.

`tests/SyringeProbe` is excluded from the main mod compile and builds as a separate test mod. It exercises the loaded registry, native container crafting and syringe volume transitions on a disposable server.
