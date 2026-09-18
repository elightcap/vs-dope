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
Heroin psychedelic watcher ─────┘
                                      │
                                      └── watched attributes
                                               │
                                               ▼
                                   AddictionCharacterTabSystem
```

## Overdose

```text
DrugConsumableItem.Consume() ─┐ (raw IntoxicationAmount)
Heroin watcher (HeroinDoseLoad)┼──> vs-dope-load-<product>  (watched)
                               │          │
                               │          ▼
                               │   MetabolizeAndCheckOverdose()  [5s tick]
                               │     ├── threshold = base + min(tol, plateau)*perPoint
                               │     ├── walkspeed slow (-min(0.85, severity))
                               │     └── poison damage past OverdoseDamageSeverityGate
                               │          │
                               │          ▼
                               │   vs-dope-overdose (watched bool)
```

Products tracked: opium, morphine, heroin, coca-vitae. Adding a new overdose-able product requires adding it to `OverdoseProducts` in `AddictionSystem.cs`.


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
| Overdose threshold/balance | `AddictionSystem.cs` (Overdose* consts) | `LoadKey`, product load writes |
| New overdose-able product | `AddictionSystem.cs` (`OverdoseProducts`) | item's `IntoxicationAmount`, tolerance key |
| New consumed product | item JSON + consumable C# | mod registration, lang, texture |
| New crop | blocktype + seed item | shapes, textures, lang, drops |
| Crop appearance | `shapes/plant/` | blocktype texture aliases |
| Crop drops | crop blocktype | item definitions |
| Addiction UI | `AddictionCharacterTabSystem.cs` | watched attribute sync |
| Coca countdown | `CocaVitaeEffectHudSystem.cs` | Coca expiry writes |
| Processing | `assets/vs-dope/recipes/` | input/output item JSON |
| Localization | `assets/vs-dope/lang/en.json` | exact asset codes |
