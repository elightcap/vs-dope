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

Centre column, top to bottom: one `game:metalrod-*` (copper/tin/brass/gold/iron/steel), one `game:clearquartz`, one `game:metalplate-*` of any metal -> one `vs-dope:syringe-empty`. Rod and plate metals are independent.

Empty syringe + one portable vessel containing at least 1 litre of heroin or morphine solution -> full syringe. The vessel remains, with exactly 1 litre removed. Native filling recipes are registered for liquid-container blocks that permit held transfers. A single vessel is required, not a stack of vessels.

Alternatively right-click a placed liquid source (including an unsealed barrel), or sneak-right-click with a single vessel in the off hand. This fills to at most 1 litre, accepts partial fills, and permits same-liquid top-ups. Mixing heroin and morphine is rejected. Morphine means the existing `vs-dope:morphine-solution` liquid, not the solid morphine item.

Hold right-click for 1.5 seconds to apply 0.1 litre. Ten applications empty a full syringe; the empty item can then be refilled with either supported liquid. Remaining volume and complete dose count appear in the tooltip.
