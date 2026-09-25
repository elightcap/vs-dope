# Poppy & Process (`vs-dope`)

A [Vintage Story](https://www.vintagestory.at/) mod (targeting **1.22.7**) that adds a poppy cultivation and drug-processing chain, complete with healing benefits, intoxicating side effects, and an addiction system that punishes regular use.

> ⚠️ This is fictional gameplay content for a survival game. It simulates drug use purely as a risk/reward mechanic — the stronger the drug, the worse the withdrawal.

## What it does

You grow poppies from seeds, harvest them, and refine the raw product into progressively more potent (and more addictive) drugs through a multi-step processing chain:

```
poppy seeds ──grow──▶ poppy plant ──harvest──▶ seedpods + seeds
                                                    │
                                  quern grind (1:1) ▼
                                                   opium
                                                    │
                    barrel (5 opium + limewater, 24h)
                                                    ▼
                                               morphine
                                                    │
               barrel (2 morphine + 1 L alcohol, 12h)
                                                    ▼
                                          morphine solution
                                                    │
                           still (10 L → 1 L)       ▼
                                                 heroin
```

Support recipe: **limewater** = lime + water in a barrel (4h). Aqua vitae is a vanilla item.

Each drug heals health but applies diminishing returns via side effects and addiction risk:

| Item | Heal | Slowdown | Intoxication | Duration | Addictive? |
|------|------|----------|--------------|----------|------------|
| Opium            | 2  | light   | yes        | 1h | yes |
| Morphine         | 5  | medium  | strong     | 2h | yes |
| Heroin           | 8  | heavy   | + psychedelic | 3h | yes |

## Addiction system

A server-side `AddictionSystem` tracks each player's use via persistent attributes:

- **Building addiction** — using a drug on consecutive in-game days raises a "days used" counter; once you hit the threshold (5), your addiction level climbs.
- **Withdrawal** — skip a day and addicted players suffer movement slowdown, scaling with severity; at high levels it deals poison damage over time.
- **Recovery** — staying clean slowly decays addiction; quitting long enough clears all effects.

Addiction persists across relogs (stored in player attributes). *Note: withdrawal state is not yet serialized across server restarts.*

## Building

Requires the Vintage Story API DLLs from a game install.

```bash
export VINTAGE_STORY=/path/to/vintagestory/install   # dir containing VintagestoryAPI.dll
dotnet build
```

This produces `bin/Debug/vs-dope.dll`.

### Installing for local testing

Copy the built DLL and assets into your mods folder:

```bash
cp bin/Debug/vs-dope.dll ~/.config/VintagestoryData/ModLoader/vs-dope/
cp -r assets/vs-dope ~/.config/VintagestoryData/ModLoader/vs-dope/
```

Restart the game for code changes (assets hot-reload, compiled code does not).

## Project structure

```
modinfo.json                 mod metadata (targets >=1.22.7, side: Both)
src/
  VsDopeModSystem.cs         entry point; boots the addiction system server-side
  Systems/AddictionSystem.cs addiction tracking, withdrawal, decay
  Items/DrugConsumableItem.cs base consumable + Opium/Morphine/Heroin variants
assets/vs-dope/
  blocktypes/poppy-plant.json    crop with growth stages and drops
  itemtypes/*.json               opium, morphine, heroin, etc.
  recipes/barrel/                limewater, opium→morphine, morphine→solution
  recipes/distilling/            solution→heroin
  statuseffects/                 withdrawal + doped effects
  shapes/, textures/             models and (placeholder) textures
```

## Status / known issues

Early work-in-progress (v0.1.0):

- Textures are placeholders — need power-of-two PNGs or the game shows missing-texture boxes.
- Vanilla item codes (`game:lime`, `game:aqua-vitae`) and the quern-grinding API should be verified against actual 1.22.7 assets.
- No server-restart persistence for withdrawal state.

## License

No license specified yet (all rights reserved).
