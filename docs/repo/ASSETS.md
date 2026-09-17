# Asset Architecture

## Crops

`assets/vs-dope/blocktypes/poppy-plant.json` defines the poppy crop and its drops.

`assets/vs-dope/blocktypes/coca-plant.json` defines the 9-stage coca crop, maps stage shapes, and produces coca leaves/seeds at maturity.

Crop naming convention: `crop` + type variant + stage -> `vs-dope:crop-{type}-{stage}`.

## Plant models

Poppy stages are `assets/vs-dope/shapes/plant/papaver_stage_01_*.json` through stage 09.

Coca stages are `assets/vs-dope/shapes/plant/coca_stage_01_*.json` through stage 09.

The papaver stages are the stronger in-repo reference for detailed plant geometry. Coca models are currently much simpler.

## Items

| Asset | Purpose | Runtime behavior |
| --- | --- | --- |
| `coca-leaf.json` | coca harvest | JSON item |
| `coca-seeds.json` | coca planting | `ItemPlantableSeed` |
| `coca-vitae.json` | coca finished product | `CocaVitaeItem` |
| `seeds.json` | poppy planting | plantable seed |
| `seedpod.json` | poppy harvest/input | processing properties |
| `opium.json` | poppy product | `OpiumItem` |
| `morphine.json` | poppy product | `MorphineItem` |
| `morphine-solution.json` | intermediate liquid | liquid behavior |
| `heroin.json` | poppy final product | special watcher path |

For custom items, inspect both JSON and C# class registration.

## Other assets

- `assets/vs-dope/lang/en.json`: localization.
- `assets/vs-dope/statuseffects/`: status-effect definitions.
- `assets/vs-dope/textures/block/`: crop/block textures.
- `assets/vs-dope/textures/item/`: item textures.

Follow the JSON/texture conventions in `AGENTS.md`.
