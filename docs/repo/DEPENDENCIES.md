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
DrugConsumableItem.Consume() ── +IntoxicationAmount ─┐
Heroin psychedelic watcher ─── +HeroinDoseLoad ──────┤
                                                     ▼
                              watched attr  vs-dope-load-<product>
                                                     │
AddictionSystem.MetabolizeAndCheckOverdose() (OnTick)┤  metabolism -MetabolismPerTick/tick
   threshold = base + min(tol,plateau)*scale         │
   severity  = (load-threshold)/threshold            ▼
        ├── stats walkspeed slow "vs-dope-overdose"
        ├── poison ReceiveDamage (sev > gate)
        └── watched bool vs-dope-overdose
```

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
| New consumed product | item JSON + consumable C# | mod registration, lang, texture |
| New crop | blocktype + seed item | shapes, textures, lang, drops |
| Crop appearance | `shapes/plant/` | blocktype texture aliases |
| Crop drops | crop blocktype | item definitions |
| Addiction UI | `AddictionCharacterTabSystem.cs` | watched attribute sync |
| Coca countdown | `CocaVitaeEffectHudSystem.cs` | Coca expiry writes |
| Overdose threshold/severity | `AddictionSystem.cs` (`MetabolizeAndCheckOverdose`, overdose consts) | per-product load attrs, tolerance plateau |
| Overdose dose accumulation | `DrugConsumableItem.Consume()` + heroin watcher | product IDs must match `OverdoseProducts` |
| Processing | `assets/vs-dope/recipes/` | input/output item JSON |
| Localization | `assets/vs-dope/lang/en.json` | exact asset codes |
