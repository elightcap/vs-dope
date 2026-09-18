# Poppy model design

The poppy uses the coca rebuild's thin, connected geometry, muted pixel textures and varied foliage orientation, with its own herbaceous silhouette. Botanical shape reference: https://en.wikipedia.org/wiki/Papaver_somniferum (lobed leaves, four petals, rounded capsule and radiating crown).

## Growth stages

1. Germination: two small leaves and a short stem.
2. Seedling: four leaves.
3. Young rosette: seven spreading leaves.
4. Vegetative: fuller rosette and initial stem leaves.
5. Bolting: taller leafy stem.
6. Closed bud: connected curved neck and hanging green bud.
7. Swollen bud: larger upright bud and one secondary stalk.
8. Flowering: two four-petalled, cupped mauve flowers, small centers and stamens.
9. Harvestable: rounded grey-green capsules with radial crowns.

Leaf silhouettes use contiguous thin sections with lobed edges and a continuous midrib. Leaves attach directly to calculated stem positions. The main stem tapers and bends subtly. The secondary stalk arises from the main stem. Bud, flower and capsule geometry remains attached to stalk endpoints. Small earth clods retain the disturbed-ground cue without dominating the plant.

## Assets and regeneration

Run `python tools/build_poppy_models.py` to regenerate all nine production `papaver_stage_*.json` files. It reuses the coca generator's segment math. Existing `block/poppy/papaver_atlas.png` and its seven texture keys are retained, with explicit tile UVs. No block definitions, drop quantities, growth timing, recipes, items or coca assets change.

Run `python tools/preview_poppy_models.py` (Pillow and NumPy) for `docs/previews/poppy-growth.png` and `poppy-mature.png`. These are software renders of the production JSONs and mapped texture atlas, using the same renderer as the coca previews. They do not simulate game lighting, wind, ambient occlusion or atlas sampling.

## Verification

Checked all nine JSON files, unique element names, positive dimensions, texture key/file resolution, UV ranges, block-to-shape mapping, transformed bounds and deterministic regeneration. Inspected the stage overview and flowering/capsule views from multiple angles.

Vintage Story is unavailable here. In-game daylight review remains necessary for foliage visibility, small stem/crown details, texture sampling and harvesting. Review the first three stages close up; their small size is intentional.
