# Progress

## 2026-09-24
- Read the guide and repo maps. Decompiled BlockContainer, BlockLiquidContainerBase, BlockBarrel, BlockEntityBarrel, ItemSlotBarrelInput, InventoryPlayerCreative, ItemType.
- Added creativeinventoryStacks to heroin.json and morphine-solution.json. Fixed the morphine-solution shape. Both files pass `python3 -m json.tool`.
- `dotnet build`: 0 errors (pre-existing warnings only). Not deployed (the coordinator deploys).
- Updated docs/repo ASSETS/PROCESSING/DEPENDENCIES.

### In-game check (user)
Creative mode, open inventory, search "bucket" / "barrel" (or open the Liquids tab): expect "Wooden bucket" entries whose tooltip shows 10 litres of Heroin / Morphine Solution, and "Barrel" entries holding 50 litres of each. Place a heroin barrel: right-click shows 50 L heroin in the liquid slot. The morphine-solution portion now renders with the vanilla liquid shape, and client logs no longer show "Did not find required shape vs-dope:shapes/item/liquid.json".
