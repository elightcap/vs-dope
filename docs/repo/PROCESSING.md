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
