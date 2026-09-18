# Asset Architecture

## Crops

`assets/vs-dope/blocktypes/poppy-plant.json` defines the poppy crop and its drops.

`assets/vs-dope/blocktypes/coca-plant.json` defines the 9-stage coca crop, maps stage shapes, and produces coca leaves/seeds at maturity.

Crop naming convention: `crop` + type variant + stage -> `vs-dope:crop-{type}-{stage}`.

## Plant models

Poppy stages are `assets/vs-dope/shapes/plant/papaver_stage_01_*.json` through stage 09. They use thin lobed leaves, a connected tapered stem, small buds, cupped four-petal flowers and rounded crowned capsules. Stage 8 flowers; stage 9 is harvestable. The existing 32×32 `block/poppy/papaver_atlas.png` and its seven texture keys are retained.

- `tools/build_poppy_models.py` regenerates all nine poppy models, reusing the coca segment geometry helper.
- `tools/preview_poppy_models.py` uses the coca software renderer for `docs/previews/poppy-growth.png` and `poppy-mature.png`.
- `docs/POPPY_MODELS.md` records the stage progression, texture contract and in-game checks.

Coca stages are `assets/vs-dope/shapes/plant/coca_stage_01_*.json` through stage 09.

Coca models use a tapered, bent leader, staggered branches, secondary twigs, and individually attached oval leaves. Stage 9 has 12 main branches and 123 leaves. Flowers begin at stage 7; fruit begins at stage 8. Shape and block texture mappings both use the existing four `coca-*-v4.png` textures.

- `tools/build_coca_models.py` regenerates all nine coca shapes deterministically.
- `tools/preview_coca_models.py` renders the actual shape geometry and mapped textures using Pillow and NumPy; results are in `docs/previews/coca-mature.png` and `coca-growth.png`.
- `docs/COCA_MODELS.md` records the design and remaining in-game checks.

Keep leaves distributed along branches and secondary twigs, with varied orientations so they remain visible from side views. Petioles overlap the transparent texture margin. Avoid reverting to thick straight trunks, bare horizontal arms, or oversized flowers/fruit.

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
