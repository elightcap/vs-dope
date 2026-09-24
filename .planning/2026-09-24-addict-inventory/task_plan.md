# Addict speed + inventory + trader-style UI (issues #38, #40)

Branch `feat/addict-inventory`, worktree `../vs-dope-addict-inventory`.

## Goal
- #38: a sprinting player clearly outruns a fleeing addict; approach ~ player walk speed.
- #38/#40: each addict carries a persistent inventory (gears + scavenged junk + drugs it buys) that drops on death.
- #40: addict pays from its own gears, then junk; trade window shows its pockets, prices, what the player holds; live refresh.

## Phases
- [x] 1. Research: movement physics (server/client), vanilla trader persistence, liquid containers, vanilla junk codes
- [x] 2. Speed fix (constants + math comment)
- [x] 3. AddictPockets (storage, stock roll, payment) + EntityDrugAddict wiring + drop on death
- [x] 4. AddictTradeSystem: affordability, payment, drugs into pockets, state refresh
- [x] 5. Packets + client dialog
- [x] 6. Lang, docs/repo maps, build 0 errors
- [x] 7. Commit

## Current phase
Done (awaiting in-game verification by user; coordinator deploys).
