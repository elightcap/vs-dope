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

## Drug stat effects

`DrugConsumableItem.ApplyDose` / `AddictionSystem.ApplyHeroinDose` -> `DrugStatEffectSystem.Apply` (product = `ToleranceProduct`, or `"heroin"`). `StonedSystem.Apply/Resume/Clear` -> `DrugStatEffectSystem.SetStats/RemoveStats(StonedStats)`. Changing a profile means updating the `drug-effects-<product>` (or `joint-tooltip`) lang text and `tests/DrugStatsProbe`. A new product needs a `Profiles` row keyed by its `ToleranceProduct` and a `drug-effects-<product>` lang key.

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
| Drug stat effects (tools/trade-offs) | `DrugStatEffectSystem.Profiles`, `StonedStats` | `drug-effects-*` lang, `tests/DrugStatsProbe` |
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

`entities/drugaddict.json` (`class` = `vs-dope.drugaddict`) -> `VsDopeModSystem.Start` `RegisterEntity` -> `Entities/EntityDrugAddict.cs`. Client `textures.skin` must match the `#skin` key in `shapes/entity/drugaddict.json`. It is also required because `deaddecay` particles read `FirstTexture`. Client `animations` codes `hurt`/`die` must exist in the shape's `animations`. Animated elements are `root` -> `torso` -> `head`, and children use parent-relative coordinates. Trade packets live in `Network/AddictTradePackets.cs`. Registration order in `AddictTradeSystem.Initialize` and `AddictTradeUiSystem.StartClientSide` must match. `AddictStackData` is a nested contract only and is not registered. The addict inventory (`Entities/AddictPockets.cs`) hard-codes vanilla item codes (`game:gear-rusty`, the junk table, `game:jug-blue-fired` for bought liquids) and drug codes (`vs-dope:opium`, `vs-dope:morphine`, `vs-dope:coca-vitae`). Renaming a drug item means updating `AddictPockets.StartingDrugs` and `AddictTradeSystem.Offers`. `tests/AddictProbe` asserts that every one of these codes resolves. Bought liquids rely on the drug's `waterTightContainerProps.itemsPerLitre`. Addict chat lines, trade-window labels and trade results are unprefixed `addict-*` lang keys (looked up as `vs-dope:addict-*`). The addict's display name comes from `item-creature-drugaddict` / `item-dead-creature-drugaddict`, which `Entity.GetName()` reads by entity code, so renaming the entity code means renaming those keys too.

## Marijuana change map

- Stage/count/growth/drop changes: `blocktypes/marijuana-plant.json`, `itemtypes/marijuana-seeds.json`, `tools/build_marijuana_models.py`, language, previews.
- Bud/joint meshes: `shapes/item/{marijuana-buds,joint}.json`, matching itemtypes and shared marijuana atlas.
- Smoking hold time: `JointItem.SmokeSeconds`, player animation patch frame duration, synthesized audio duration/trigger, `tests/MarijuanaProbe`, `docs/MARIJUANA.md`. Current user requirement: **5 seconds**.
- Smoking pose/keyframes: `patches/marijuana-player.json` (both Seraph shapes, both camera variants) and `tests/MarijuanaProbe/SmokingAnimationChecks.cs`. Supply complete XYZ transform groups and run native frame generation to catch first-playback failures.
- Stoned keys/timing: `Systems/StonedSystem.cs`, `Client/StonedHudSystem.cs`, `Client/StonedEyesBehavior.cs`, localization.
- Seed access: separate `patches/marijuana-seed-traders.json`, using the same vanilla lists as coca seeds.
- Eye cosmetics: register behavior in `VsDopeModSystem`, append after skin/inventory in player patch; retain VSEssentials reference and exact sclera mask.
`entities/drugaddict.json` (`class` = `vs-dope.drugaddict`) -> `VsDopeModSystem.Start` `RegisterEntity` -> `Entities/EntityDrugAddict.cs`. Client shape is vanilla `game:entity/humanoid/seraph-faceless` plus `shape.overlays` `seraphskinparts/face/tired` and `seraphskinparts/hair-base/messy2`. Face and hair must come in as overlays: their root elements are step-parented to `Head`, and a plain entity only step-parents overlays (`Entity.OnTesselation`); using `seraph.json` directly renders its face and hair at the feet. Client `textures` keys must be `seraph` (our `textures/entity/drugaddict.png`, 64x152 seraph UV layout, generated by `tools/build_addict_texture.py`) and `hair`; keep `seraph` first because `deaddecay` particles read `FirstTexture`. Every client `animations[].animation` must exist in the seraph shape's `animations`. The `walk`/`sprint`/`idle` triggers depend on `EntityDrugAddict` setting `Controls.Forward`, and `Controls.Sprint` while fleeing (Sprint doubles ground speed via `GetWalkSpeedMultiplier`, which `Walk()` divides back out). Trade packets live in `Network/AddictTradePackets.cs`. Registration order in `AddictTradeSystem.Initialize` and `AddictTradeUiSystem.StartClientSide` must match. `AddictStackData` is a nested contract only and is not registered. The addict inventory (`Entities/AddictPockets.cs`) hard-codes vanilla item codes (`game:gear-rusty`, the junk table, `game:jug-blue-fired` for bought liquids) and drug codes (`vs-dope:opium`, `vs-dope:morphine`, `vs-dope:coca-vitae`). Renaming a drug item means updating `AddictPockets.StartingDrugs` and `AddictTradeSystem.Offers`. `tests/AddictProbe` asserts that every one of these codes resolves. Bought liquids rely on the drug's `waterTightContainerProps.itemsPerLitre`. Addict chat lines, trade-window labels and trade results are unprefixed `addict-*` lang keys (looked up as `vs-dope:addict-*`). The addict's display name comes from `item-creature-drugaddict` / `item-dead-creature-drugaddict`, which `Entity.GetName()` reads by entity code, so renaming the entity code means renaming those keys too.
