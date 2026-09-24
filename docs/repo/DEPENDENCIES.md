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

`RecordDrugDose` -> tolerance before this dose -> `OverdoseSystem.RecordDose` (prune window, roll per dose, worsen severity) -> `RecordUse` / `RecordToleranceUse`. `OverdoseSystem.Tick` -> window pruning + calendar-based severity recovery -> one movement correction and poison damage -> watched risk/severity/active/recent keys -> `Client/OverdoseHudSystem`.

Products: opium, morphine, heroin, coca-vitae. Adding a product requires a row in `OverdoseSystem.Risks` and `DrugVisualEffects.PerDose`, and its `ToleranceProduct`. See `docs/OVERDOSE.md` for persistence and balance.

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
| Overdose chance/balance | `OverdoseSystem.cs` (`Risks`, constants) | `RecordDrugDose`, HUD warning, `tests/OverdoseProbe` |
| New overdose-able product | `OverdoseSystem.Risks`, `DrugVisualEffects.PerDose` | tolerance key |
| Drug screen-effect strength | `DrugVisualEffects.cs` | `heroin.json` nutritionPropsPerLitre (kept in sync) |
| New consumed product | item JSON + consumable C# | mod registration, lang, texture |
| New liquid / creative filled vessel | liquid itemtype `creativeinventoryStacks` (`game:woodbucket`, `game:barrel`, `ucontents` with `vs-dope:` code) | `waterTightContainerProps`, shape `game:item/liquid`, lang |
| New crop | blocktype + seed item | shapes, textures, lang, drops |
| Crop appearance | `shapes/plant/` | blocktype texture aliases |
| Crop drops | crop blocktype | item definitions |
| Addiction UI | `AddictionCharacterTabSystem.cs` | watched attribute sync |
| Coca countdown | `CocaVitaeEffectHudSystem.cs` | Coca expiry writes |
| Overdose severity/recovery/damage | `OverdoseSystem.cs` (`Apply`, `Advance`) | HUD severity gate |
| Overdose dose counting | `RecordDrugDose`, `HeroinVesselDoseSystem` | consumed volume (0.1 L per roll), product IDs in `OverdoseSystem.Risks` |
| Processing | `assets/vs-dope/recipes/` | input/output item JSON |
| Localization | `assets/vs-dope/lang/en.json` | exact asset codes |

## Syringes

`itemtypes/syringe.json` -> `VsDopeModSystem` registration -> `Items/SyringeItem.cs` -> `MorphineItem.ApplyDose` / `AddictionSystem.ApplyHeroinSyringeDose`. `SyringeRecipeSystem` discovers liquid-container blocks and registers filling recipes; empty crafting is JSON. Content variants map to the three syringe texture PNGs and English localization. Liquid volume relies on the current heroin and morphine-solution `itemsPerLitre: 100` contract; unsupported densities are rejected. Native container types require the VSSurvivalMod assembly reference.

`tests/SyringeProbe` is excluded from the main mod compile and builds as a separate test mod. It exercises the loaded registry, native container crafting and syringe volume transitions on a disposable server.

## Drug addict

`entities/drugaddict.json` (`class` = `vs-dope.drugaddict`) -> `VsDopeModSystem.Start` `RegisterEntity` -> `Entities/EntityDrugAddict.cs`. Client `textures.skin` must match the `#skin` key in `shapes/entity/drugaddict.json`. It is also required because `deaddecay` particles read `FirstTexture`. Client `animations` codes `hurt`/`die` must exist in the shape's `animations`. Animated elements are `root` -> `torso` -> `head`, and children use parent-relative coordinates. Trade packets live in `Network/AddictTradePackets.cs`. Registration order in `AddictTradeSystem.Initialize` and `AddictTradeUiSystem.StartClientSide` must match. Addict chat lines, trade-window labels and trade results are unprefixed `addict-*` lang keys (looked up as `vs-dope:addict-*`). The addict's display name comes from `item-creature-drugaddict` / `item-dead-creature-drugaddict`, which `Entity.GetName()` reads by entity code, so renaming the entity code means renaming those keys too.
