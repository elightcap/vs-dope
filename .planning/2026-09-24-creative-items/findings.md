# Findings

## Root cause
- `heroin.json` / `morphine-solution.json` only had `creativeinventory: { general: ["*"] }`, which lists the raw liquid portion item. Nothing listed filled containers.
- Vanilla puts filled containers in creative from the **liquid** itemtype, not from the container: e.g. `survival/itemtypes/liquid/alcohol.json`:
  `creativeinventoryStacks: [ { tabs: ["general","liquids"], stacks: [ { type:"block", code:"woodbucket", attributes:{ ucontents:[ { type:"item", code:"alcoholportion", makefull:true } ] } } ] } ]`.
  Vanilla `wood/bucket.json` lists only the empty bucket. No vanilla file lists a filled barrel.

## Verified API (decompiled, 1.22.7)
- `InventoryPlayerCreative` adds `creativeinventory` tabs AND `CreativeInventoryStacks` together (additive), and logs a warning for unresolved stacks.
- `BlockContainer.ResolveUcontents` -> `CreateItemStackFromJson(stackAttr, world, this.Code.Domain)`, so a bare code resolves in the *container's* domain (`game`). Our liquid codes need the `vs-dope:` prefix.
- `BlockLiquidContainerBase.CreateItemStackFromJson`: `makefull` sets the stack size to `CapacityLitres * ItemsPerLitre`. The barrel holds 50 L, so 5000 portions, which equals our `maxStackSize` 5000.
- `BlockBarrel : BlockLiquidContainerBase`, slot id 1 = liquid. `BlockEntityContainer.OnBlockPlaced` copies the item contents to slot 0, then `BlockEntityBarrel.OnBlockPlaced` flips a liquid in slot 0 into slot 1. So a single-entry `ucontents` barrel works when placed.
- Barrel inventory = `ItemSlotBarrelInput` (0) + `ItemSlotLiquidOnly` (1): it cannot hold two different liquids.

## Decisions
- Put the stacks on the liquid itemtypes (the vanilla pattern) rather than patching vanilla bucket/barrel. This keeps the change local to our files and matches vanilla exactly.
- Keep the existing raw-portion `creativeinventory` entries (minimal change; other tasks may rely on them).
- Fixed `morphine-solution.json` shape `item/liquid` -> `game:item/liquid`. The log showed "Did not find required shape vs-dope:shapes/item/liquid.json".
- **No heroin barrel recipe added.** Heroin comes from distilling morphine solution (`distillationProps`, ratio 0.1, the vanilla cider->spirit pattern). A "morphine solution + alcohol" barrel recipe needs two liquids in one barrel, which the barrel inventory cannot hold. CLAUDE.md's chain line ("barrel + alcohol (6h) -> heroin") is inaccurate.
