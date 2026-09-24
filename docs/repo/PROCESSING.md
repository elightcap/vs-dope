# Processing Chains

This file describes fictional game mechanics, not real-world processing instructions.

## Poppy chain

```text
Poppy crop
  -> seedpod
  -> quern (1 seedpod -> 2 opium)
  -> opium
  -> morphine
  -> morphine solution
  -> heroin
```

Seedpods use Vintage Story grinding properties, so the conversion appears as a quern recipe.

Recipe locations: opium -> morphine is `recipes/barrel/opium-to-morphine.json` (1 opium + 1 L limewater, 24 h). Morphine -> morphine solution is `recipes/barrel/morphine-to-solution.json` (1 morphine + 1 L alcohol, 12 h). Morphine solution -> heroin is **not** a barrel recipe. It is `distillationProps` on `itemtypes/morphine-solution.json` (still, ratio 0.1, like vanilla cider -> spirit). A "morphine solution + alcohol" barrel recipe cannot work, because a barrel has one solid slot (`ItemSlotBarrelInput`) and one liquid slot (`ItemSlotLiquidOnly`), so it cannot hold two different liquids.

Filled buckets and barrels of heroin and morphine solution appear in creative (see `ASSETS.md`, "Liquids in the creative inventory").

## Coca chain

```text
Coca crop
  -> coca leaf
  -> sealed barrel with an equal amount of Aqua Vitae
  -> coca paste
  -> Dry transition
  -> Coca Vitae
```

The barrel recipe is defined at a 1:1 game ratio: one coca leaf plus one litre of Aqua Vitae produces one coca paste. Barrel recipes scale, so 50 leaves plus 50 litres produce 50 paste. Coca paste then uses the game's `Dry` transition to become Coca Vitae at a 1:1 ratio.

The barrel recipe uses Vintage Story's Aqua Vitae liquid (`game:aquavitaeportion`).

Keep processing fictional and game-mechanical; do not document real-world extraction chemistry.


## Syringes

Centre column, top to bottom: one `game:rod-*` (copper/tin/brass/gold/iron/steel), one `game:clearquartz`, one `game:metalplate-*` of any metal -> one `vs-dope:syringe-empty`. Rod and plate metals are independent.

Empty syringe + one portable vessel containing at least 1 litre of heroin or morphine solution -> full syringe. The vessel remains, with exactly 1 litre removed. Native filling recipes are registered for liquid-container blocks that permit held transfers. A single vessel is required, not a stack of vessels.

Alternatively right-click a placed liquid source (including an unsealed barrel), or sneak-right-click with a single vessel in the off hand. This fills to at most 1 litre, accepts partial fills, and permits same-liquid top-ups. Mixing heroin and morphine is rejected. Morphine means the existing `vs-dope:morphine-solution` liquid, not the solid morphine item.

Hold right-click for 1.5 seconds to apply 0.1 litre. Ten applications empty a full syringe; the empty item can then be refilled with either supported liquid. Remaining volume and complete dose count appear in the tooltip.

Tin and brass rods are added to the vanilla rod variants and rod smithing recipe by `patches/syringe-rod-metals.json`; the other four requested rod metals already exist in 1.22.7.

## Marijuana chain

Trader/creative marijuana seeds -> native nine-stage crop -> stage-9 Marijuana buds + seeds -> shapeless grid (one bud + one `game:paper-parchment`) -> one Joint. Both finished item models are 3D.

Hold right-click for **five seconds** to smoke one Joint. Stoned: two calendar hours, -20% base movement, +0.5 HP per game minute, +25% hunger and -25% creature detection factor. Marijuana-specific tolerance scales these magnitudes; reuse refreshes without stacking. There is no extra drying step or overdose roll in this chain. See `docs/MARIJUANA.md` and `docs/DRUG_TOOLS.md`.
