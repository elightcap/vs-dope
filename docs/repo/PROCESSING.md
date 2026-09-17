# Processing Chains

This file describes game mechanics, not real-world processing instructions.

## Poppy chain

Conceptual game flow:

```text
Poppy crop
  -> seedpod
  -> opium
  -> morphine
  -> morphine solution
  -> heroin
```

Current repository recipe files include:

```text
assets/vs-dope/recipes/barrel/opium-to-morphine.json
assets/vs-dope/recipes/barrel/morphine-to-solution.json
```

Do not assume older README diagrams correspond to implemented files. Inspect `assets/vs-dope/recipes/` before modifying processing.

## Coca chain

```text
Coca crop
  -> coca leaf
  -> [fictional game processing path still requires validated recipe support]
  -> Coca Vitae
```

Coca Vitae exists as an item/runtime effect, but the leaf-to-finished-product recipe path still needs a validated Vintage Story-compatible implementation.

Keep any future processing fictional and game-mechanical; do not document real-world extraction chemistry.
