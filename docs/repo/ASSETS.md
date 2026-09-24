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

## Drug addict entity

`assets/vs-dope/entities/drugaddict.json` renders with the **vanilla seraph body** `game:entity/humanoid/seraph-faceless` (the same body, rig and animation set as the player) plus two `shape.overlays`, vanilla `seraphskinparts/face/tired` and `seraphskinparts/hair-base/messy2`. The overlays are required: face/hair elements are step-parented to `Head`, and only overlays get step-parented for a plain entity. Using `seraph.json` directly puts the face and hair on the ground at the feet, so no addict shape lives in this repo any more. The old blocky placeholder shape `shapes/entity/drugaddict.json` was removed.

- Texture keys must match the seraph shape: `seraph` -> `vs-dope:entity/drugaddict` (our skin; listed first because `deaddecay` particles read `FirstTexture`), `hair` -> `game:entity/humanoid/seraphskinparts/hair/rust3` (greasy dark brown).
- `textures/entity/drugaddict.png` is **64x152**, the same size as vanilla seraph body skins (`seraphskinparts/body/skin*.png`): the shape's `textureSizes.seraph` is 32x76 UV units and the texture must keep that aspect ratio. This is an intentional exception to the power-of-two rule; vanilla entity skins are not power-of-two either.
- `tools/build_addict_texture.py` regenerates it deterministically from vanilla `skin18` and the UVs it reads from `$VINTAGE_STORY/assets/game/shapes/entity/humanoid/seraph.json`: sallow grimy skin, eye rings, stubble, dull eyes, forearm track marks and sores, stained torn linen shirt with ragged short sleeves, patched trousers torn off at the shin, rag-wrapped feet. `--preview out.png` writes a flat front/back/texture sheet; the committed one is `docs/previews/drugaddict.png`.
- The face is the head's **west** face; the eye/eyelid/mouth/brow elements sample the strip at x 56-63, y 0-27 of the texture.
- Animations (all codes exist in the seraph shape): `walk` (Move), `sprint` (Move+SprintMode), `idle` -> `coldidle` shiver as the default anim (Idle), `jump` -> `newjump`, `swim`, `swimidle`, `hurt`, `die` (Dead). Triggers key off `EnumEntityActivity`, which the client derives from the server's synced `Controls` (`Forward` => Move, `Sprint` => SprintMode), so `EntityDrugAddict.Walk()` setting `Controls.Forward` is what plays `walk`.

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
| `morphine-solution.json` | intermediate liquid | `ItemLiquidPortion`; distils to heroin; creative bucket/barrel stacks |
| `heroin.json` | poppy final product | `ItemLiquidPortion`; explicit vessel/syringe dose path; creative bucket/barrel stacks |

For custom items, inspect both JSON and C# class registration.

### Liquids in the creative inventory

The two liquids follow the vanilla liquid pattern (`$VINTAGE_STORY/assets/survival/itemtypes/liquid/*.json`): the liquid itemtype itself declares `creativeinventoryStacks` holding a filled container. Vanilla containers do not list their own filled variants. Each liquid lists `game:woodbucket` and `game:barrel` with `attributes.ucontents: [{ type: "item", code: "vs-dope:<liquid>", makefull: true }]` in the `general` and `liquids` tabs. `BlockContainer.ResolveUcontents` resolves `ucontents` with the **container's** domain (`game`), so the liquid code must keep the `vs-dope:` prefix. `makefull` fills to the container's `CapacityLitres` (bucket 10 L, barrel 50 L, so 5000 portions at 100/L, which equals `maxStackSize`). A barrel places the single content stack into its solid slot, and `BlockEntityBarrel.OnBlockPlaced` then moves it to the liquid slot. The raw portion items remain listed through `creativeinventory`.

The liquid shape must be `game:item/liquid`. A bare `item/liquid` resolves to `vs-dope:shapes/item/liquid.json`, which does not exist.

## Other assets

- `assets/vs-dope/lang/en.json`: localization.
- `assets/vs-dope/statuseffects/`: status-effect definitions.
- `assets/vs-dope/textures/block/`: crop/block textures.
- `assets/vs-dope/textures/item/`: item textures.

Follow the JSON/texture conventions in `AGENTS.md`.


## Syringe and opium inventory artwork

`itemtypes/syringe.json` registers empty, heroin and morphine variants with matching 64x64 RGBA item textures and a stack limit of one. `recipes/grid/syringe-empty.json` crafts one empty syringe from a rod, clear quartz, and plate in the centre column. Rod and plate wildcard names are independent, so their metals need not match.

`itemtypes/opium.json` now uses `textures/item/opium-paste-icon.png` as an extruded inventory sprite. The existing item code, quern yield, processing and effects are preserved; no separate paste ingredient is added. Legacy opium shape/texture assets remain available. The new artwork was generated with the built-in image generator and normalized to power-of-two 64px textures; see `docs/SYRINGES.md` for the prompt set.

`patches/syringe-rod-metals.json` adds tin and brass variants to the vanilla `game:rod-*` item family and smithing recipe so all six requested metals are available on 1.22.7. It adds their density, recycling properties and English names.
