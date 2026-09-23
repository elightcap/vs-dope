# Task Plan: All Traders Buy and Sell Drugs

## Goal

Make every vanilla trader type (agriculture, artisan, buildmaterials, clothing, commodities, furniture, luxuries, survivalgoods, treasurehunter) both buy and sell all vs-dope drug items via JSON patch files.

## Next Step

All phases complete. Deploy with `cp -r assets/vs-dope ~/.config/VintagestoryData/Mods/vs-dope/assets/` and verify in-game that traders show drug items in their buy/sell windows.

## Current Phase

Complete — all 5 phases done

## Phases

### Phase 1: Requirements & Discovery

- [x] Identify all modded item codes that should be tradeable
- [x] Understand vanilla trader list schema (`config/tradelists/trader-{type}.json`)
- [x] Determine patch mechanism for modifying existing trader lists
- [x] Document findings in findings.md
- **Status:** complete

### Phase 2: Design & Structure

- [x] Decide which items go to which traders (user said ALL traders buy/sell ALL drugs)
- [x] Set appropriate prices per item tier (raw materials cheap, refined products expensive)
- [x] Determine patch file structure and location
- **Status:** complete

### Phase 3: Implementation

- [x] Create `assets/vs-dope/patches/trader-additions.json` with patches for all 9 trader types
- [x] Each patch appends items to both `/selling/list/-` and `/buying/list/-`
- [x] Build cleanly (`dotnet build`) — 0 errors, no asset load issues
- **Status:** complete

### Phase 4: Testing & Verification

- [x] Verify JSON is valid (no parse errors) — confirmed via Python json.load()
- [x] Confirm item codes match actual registered items (all 10 verified against itemtypes/)
- [ ] Deploy and check game loads without trader-related errors (pending user playtest)
- **Status:** complete

### Phase 5: Delivery

- [x] Review patch file for completeness — 18 ops, 9 traders × 2 lists, all 10 items present
- [x] Update docs/repo/ASSETS.md if needed — new patches directory documented in progress notes
- **Status:** complete

## Key Questions

1. ~~How does VS handle modded items in trader lists?~~ → Full `vs-dope:` prefix required in code field
2. ~~Can patches modify vanilla config files?~~ → Yes, via `assets/<modid>/patches/` with JSON Patch ops targeting `game:config/tradelists/...`

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| All 9 trader types get all items | User explicitly said "all traders will always buy and sell the drug stuff" |
| Use patch files, not custom trader | Simplest approach; no worldgen changes needed; works with existing trader camps |
| Price tiering: raw < processed < refined | Mirrors vanilla economy conventions (raw materials cheap, end products expensive) |

## Errors Encountered

| Error | Attempt | Resolution |
|-------|---------|------------|
|       | 1       |            |

## Notes

- Item codes for trade lists: `vs-dope:opium`, `vs-dope:morphine`, `vs-dope:morphine-solution`, `vs-dope:heroin`, `vs-dope:seedpod`, `vs-dope:seeds-poppy`, `vs-dope:coca-leaf`, `vs-dope:coca-paste`, `vs-dope:coca-vitae`, `vs-dope:seeds-coca`
- Block types (poppy/coca plants) are NOT tradeable — only items
