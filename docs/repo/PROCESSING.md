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
  -> sealed barrel with equal game-units of distilled spirits
  -> coca paste
  -> Dry transition
  -> Coca Vitae
```

The barrel recipe is defined at a 1:1 game ratio: one coca leaf plus one litre of the game's generic alcohol portion produces one coca paste. Barrel recipes scale, so 50 leaves plus 50 litres produce 50 paste. Coca paste then uses the game's `Dry` transition to become Coca Vitae at a 1:1 ratio.

The repository currently uses vanilla `game:alcoholportion` as the distilled-spirit liquid because base Vintage Story 1.22.7 does not provide a liquid item named `aqua-vitae`.

Keep processing fictional and game-mechanical; do not document real-world extraction chemistry.
