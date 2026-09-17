# Repository Map

> Maintainer/agent navigation guide for `vs-dope`. Update this file whenever a feature adds a new subsystem, asset family, processing chain, or important cross-file dependency.

## Quick orientation

This is a compiled Vintage Story 1.22.7 mod targeting .NET 10.

- **Mod ID:** `vs-dope`
- **C# namespace:** `VsDope`
- **Entry point:** `src/VsDopeModSystem.cs`
- **Gameplay assets:** `assets/vs-dope/`
- **Core runtime systems:** `src/Systems/`
- **Consumable behavior:** `src/Items/`
- **Client UI:** `src/Client/`
- **Build project:** `vs-dope.csproj`
- **Developer rules/gotchas:** `AGENTS.md`

When changing gameplay, first determine whether the behavior is JSON-driven, C#-driven, or both.

## Top-level map

```text
vs-dope/
├── AGENTS.md                         developer conventions and known API gotchas
├── README.md                         user-facing overview; some older details may lag implementation
├── modinfo.json                      Vintage Story mod metadata
├── vs-dope.csproj                    .NET 10 project; VS DLL paths use VINTAGE_STORY
├── docs/
│   ├── REPOSITORY_MAP.md             this navigation guide
│   └── COCA_VITAE_FIXES.md           coca implementation review/follow-up notes
├── src/
│   ├── VsDopeModSystem.cs            mod entry point / item-class registration
│   ├── Items/
│   │   └── DrugConsumableItem.cs     shared drug consumption/effects + item subclasses
│   ├── Systems/
│   │   └── AddictionSystem.cs        addiction, withdrawal, tolerance, heroin watcher
│   └── Client/
│       ├── AddictionCharacterTabSystem.cs
│       └── CocaVitaeEffectHudSystem.cs
└── assets/vs-dope/
    ├── blocktypes/                   crop definitions
    ├── itemtypes/                    item definitions and C# class bindings
    ├── lang/                         localization
    ├── recipes/barrel/               current JSON processing recipes
    ├── shapes/
    │   ├── block/                    legacy/block shapes
    │   └── plant/                    stage-specific crop models
    ├── statuseffects/                status-effect JSON
    └── textures/
        ├── block/
        └── item/
```

## Runtime architecture

### `src/VsDopeModSystem.cs`
Start here for C# registration questions.

Responsibilities:
- Registers custom item classes used by JSON.
- Creates and initializes `AddictionSystem` server-side.
- Provides the static `VsDopeModSystem.AddictionSystem` reference used by consumables.

If a JSON item's `class` value is changed or a new custom item class is introduced, registration usually belongs here.

### `src/Items/DrugConsumableItem.cs`
Primary path for directly consumed custom items.

`DrugConsumableItem` owns:
- held interaction/eat animation;
- consumption timing;
- healing;
- intoxication/psychedelic watched attributes;
- movement-speed modifiers;
- effect expiration;
- addiction recording;
- product-specific tolerance lookup/recording.

Current subclasses:
- `OpiumItem`
- `MorphineItem`
- `CocaVitaeItem`

Important: tolerance is identified by `ToleranceProduct`, not merely by the C# class. Keep this stable if renaming an asset, otherwise existing player tolerance data may effectively reset.

Coca Vitae additionally writes its effect expiry to watched attributes for the client HUD.

### `src/Systems/AddictionSystem.cs`
Server-side persistent player-use system.

Owns:
- addiction level;
- consecutive-use tracking;
- withdrawal;
- addiction decay;
- product-specific tolerance;
- tolerance recovery;
- heroin detection/effects.

Tolerance design:
- first 2 uses of a product per in-game day do not add tolerance;
- heavier same-day use adds progressively more;
- tolerance is isolated per product;
- unused days recover tolerance;
- max tolerance is capped;
- beneficial effects retain a minimum effectiveness.

Heroin is a special case: its item JSON is not currently a `DrugConsumableItem`; the system watches changes to the player's psychedelic attribute to infer use. Treat changes to this path carefully.

### `src/Client/AddictionCharacterTabSystem.cs`
Adds an Addiction tab to the character UI and reads watched attributes synchronized by `AddictionSystem`.

### `src/Client/CocaVitaeEffectHudSystem.cs`
Displays the active Coca Vitae countdown. It reads the watched calendar-hour expiry written by `CocaVitaeItem`.

When changing Coca Vitae's effect key or expiry attribute naming, update both the item and this HUD.

## Asset architecture

### Crops

`assets/vs-dope/blocktypes/poppy-plant.json`
- Poppy crop definition.
- Uses papaver stage shapes.
- Produces the raw inputs for the poppy processing chain.

`assets/vs-dope/blocktypes/coca-plant.json`
- Coca crop definition.
- 9 stages.
- Stage shapes live under `shapes/plant/coca_stage_*.json`.
- Produces `coca-leaf` and coca seeds at maturity.
- Coca plant visuals are currently much simpler than the papaver models.

Crop naming convention:
`crop` + type variant + stage -> `vs-dope:crop-{type}-{stage}`.

### Plant models

Poppy:
`assets/vs-dope/shapes/plant/papaver_stage_01_...json` through stage 09.

Coca:
`assets/vs-dope/shapes/plant/coca_stage_01_...json` through stage 09.

The papaver shapes are the better in-repo reference when implementing more detailed plant geometry.

### Item definitions

Important mappings:

| Asset | Purpose | Runtime behavior |
| --- | --- | --- |
| `coca-leaf.json` | coca harvest | JSON item |
| `coca-seeds.json` | coca planting | `ItemPlantableSeed` |
| `coca-vitae.json` | coca finished product | `CocaVitaeItem` |
| `seeds.json` | poppy planting | plantable seed |
| `seedpod.json` | poppy harvest/input | includes processing properties |
| `opium.json` | poppy product | `OpiumItem` |
| `morphine.json` | poppy product | `MorphineItem` |
| `morphine-solution.json` | intermediate liquid | liquid item behavior |
| `heroin.json` | poppy final product | liquid portion / special watcher path |

Check both the JSON and its C# registration before changing a custom item.

## Processing chains

### Poppy chain

Current intended conceptual flow:

```text
Poppy crop
  -> seedpod
  -> opium
  -> morphine
  -> morphine solution
  -> heroin
```

Current repository recipe files only include:

```text
recipes/barrel/opium-to-morphine.json
recipes/barrel/morphine-to-solution.json
```

Do not assume README diagrams represent files that actually exist. Inspect `assets/vs-dope/recipes/` before changing processing.

### Coca chain

```text
Coca crop
  -> coca leaf
  -> [fictional processing integration still requires validated recipe support]
  -> Coca Vitae
```

Do not add real-world extraction chemistry. Keep processing fictional and game-mechanical.

## Cross-file dependency map

### Coca Vitae

```text
assets/vs-dope/itemtypes/coca-vitae.json
        │ class
        ▼
src/VsDopeModSystem.cs
        │ registers
        ▼
src/Items/DrugConsumableItem.cs :: CocaVitaeItem
        ├── calls -> AddictionSystem.GetEffectMultiplier()
        ├── calls -> AddictionSystem.RecordUse()
        ├── calls -> AddictionSystem.RecordToleranceUse()
        └── writes watched expiry
                       │
                       ▼
src/Client/CocaVitaeEffectHudSystem.cs
```

### Addiction/tolerance

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

### Crops

```text
seed item -> plantBlockCode -> crop blocktype
                               │
                               ├── shapeByType -> stage shape JSON
                               ├── textures -> texture assets
                               └── drops -> harvested item JSON
```

## Where to make common changes

| Goal | Primary file(s) | Also inspect |
| --- | --- | --- |
| Change Coca Vitae stats | `src/Items/DrugConsumableItem.cs` | Coca HUD, tolerance behavior |
| Change tolerance balance | `src/Systems/AddictionSystem.cs` | all finished products |
| Add new consumed product | item JSON + `DrugConsumableItem.cs` | `VsDopeModSystem.cs`, lang, texture |
| Add new crop | blocktype + seed item | shapes, textures, lang, drops |
| Change crop appearance | `shapes/plant/` | blocktype texture aliases |
| Change crop drops | crop blocktype | item definitions |
| Change addiction UI | `AddictionCharacterTabSystem.cs` | watched attribute sync |
| Change Coca countdown | `CocaVitaeEffectHudSystem.cs` | Coca expiry writes |
| Change processing | `assets/vs-dope/recipes/` | input/output item JSON |
| Add localization | `assets/vs-dope/lang/en.json` | exact asset codes |

## Current known maintenance concerns

1. **README drift.** README contains historical processing/layout details that do not exactly match the current tree. Treat code/assets as authoritative and update README when features stabilize.
2. **Coca processing gap.** Coca Vitae exists as an item, but the leaf-to-finished-product recipe path still needs a validated Vintage Story-compatible implementation.
3. **Coca art quality.** Coca stage models are substantially simpler than papaver stages and use placeholder/reused textures.
4. **Heroin special case.** Heroin tolerance/use is inferred through psychedelic changes rather than the shared consumable base class.
5. **Effect synchronization.** Server-side effect expiry and client watched attributes must stay consistent.
6. **Build dependency.** Local builds require `VINTAGE_STORY` to point at a 1.22.x installation containing the required DLLs.

## Safe navigation workflow

Before modifying a subsystem:

1. Read this map and `AGENTS.md`.
2. Fetch the current `master` tree; do not rely on an old branch map.
3. Inspect the relevant JSON and C# files together.
4. Search for the asset code, C# class name, stat key, and watched-attribute key before renaming anything.
5. Create a feature/fix branch from current `master`; repository rules require PR-based changes.
6. Document behavioral/balance changes in the PR.
7. Build/test against the target Vintage Story API when API-facing code changes.
8. Update this map if the architecture or processing chain changed.

## Naming/index

Useful search terms:

- `coca-vitae` — finished coca product and tolerance ID
- `vs-dope-coca-vitae-speed` — Coca Vitae movement effect key
- `ToleranceProduct` — per-product tolerance routing
- `RecordToleranceUse` / `GetEffectMultiplier` — tolerance API
- `psychedelic` — heroin watcher path
- `crop-coca` / `crop-poppy` — crop codes
- `plantBlockCode` — seed-to-crop linkage
- `shapeByType` — growth-stage shape mapping
- `RegisterItemClass` — JSON custom-class registration
