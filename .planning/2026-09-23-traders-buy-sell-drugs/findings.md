# Findings & Decisions

## Requirements

- All traders must buy AND sell all drug items
- Should work with existing vanilla trader camps (no new structures)
- Items: opium, morphine, morphine-solution, heroin, seedpod, seeds-poppy, coca-leaf, coca-paste, coca-vitae, seeds-coca

## Research Findings

### Trader List Schema (`config/tradelists/trader-{type}.json`)

```json
{
  "money": { "avg": N, "var": N },
  "selling": { "maxItems": N, "list": [...] },
  "buying":  { "maxItems": N, "list": [...] }
}
```

Each list entry:
```json
{
  "code": "<domain:item-code>",
  "type": "item" | "block",
  "stacksize": N,
  "stock": { "avg": N, "var": N },
  "price": { "avg": N, "var": N }
}
```

### Vanilla Trader Types (9 total)

| File | Type | Typical Price Range |
|------|------|-------------------|
| trader-agriculture.json | Agriculture | 1-15 |
| trader-artisan.json | Artisan | 3-20 |
| trader-buildmaterials.json | Build Materials | 1-10 |
| trader-clothing.json | Clothing | 4-15 |
| trader-commodities.json | Commodities | 1-8 |
| trader-furniture.json | Furniture | 5-30 |
| trader-luxuries.json | Luxuries | 6-60 |
| trader-survivalgoods.json | Survival Goods | 2-12 |
| trader-treasurehunter.json | Treasure Hunter | 8-40 |

### Patch System

Patch files go in `assets/<modid>/patches/` with any `.json` filename. Format:
```json
[
  {
    "file": "game:config/tradelists/trader-luxuries",
    "op": "add",
    "side": "server",
    "path": "/selling/list/-",
    "value": [ ... entries to append ... ]
  }
]
```

The `"code"` field in trade entries uses full domain prefix for modded items: `vs-dope:opium`.

### Item Codes (from itemtypes/*.json)

| File | Code | Variants | Full Trade Code(s) |
|------|------|----------|-------------------|
| opium.json | opium | none | vs-dope:opium |
| morphine.json | morphine | none | vs-dope:morphine |
| morphine-solution.json | morphine-solution | none | vs-dope:morphine-solution |
| heroin.json | heroin | none | vs-dope:heroin |
| seedpod.json | seedpod | none | vs-dope:seedpod |
| seeds.json | seeds | type=[poppy] | vs-dope:seeds-poppy |
| coca-leaf.json | coca-leaf | none | vs-dope:coca-leaf |
| coca-paste.json | coca-paste | none | vs-dope:coca-paste |
| coca-vitae.json | coca-vitae | none | vs-dope:coca-vitae |
| coca-seeds.json | seeds | type=[coca] | vs-dope:seeds-coca |

### Reference Files

- `/opt/vintagestory/assets/survival/config/tradelists/` — all vanilla trader lists
- Vanilla entries use bare codes (no `game:` prefix needed for same-domain items)

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| Patch existing traders via JSON patches | No C# code, no worldgen changes; works immediately with all spawned trader camps |
| All 10 items in every trader (both buy+sell) | User explicitly requested "all traders always buy and sell" |
| Single patch file for simplicity | Easier to maintain than 9 separate files; one JSON array of patch operations |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
|       |            |

## Resources

- `/opt/vintagestory/assets/survival/config/tradelists/` — vanilla trader definitions
- `docs/repo/ASSETS.md` — mod asset reference
- VS wiki on JSON patches: uses RFC 6902-style ops with `"file"` targeting an AssetLocation

---

*Update this file regularly during research so important evidence remains available after context changes.*
