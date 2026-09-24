# Progress

## 2026-09-24
- Created worktree/branch. Decompiled PModuleOnGround, PModuleMotionDrag, EntityBehaviorControlledPhysics,
  EntityBehaviorPlayerPhysics, EntityControls.CalcMovementVectors, EntityAgent/EntityPlayer.GetWalkSpeedMultiplier,
  server PhysicsManager (1/30 tick). Speed math in findings.md.
- Decompiled EntityTradingHumanoid persistence, Entity.ToBytes/Die/GetDrops, BlockLiquidContainerBase,
  PlayerInventoryManager.TryGiveItemstack (leaves remainder in stack.StackSize), GuiDialogTrader layout.
- Verified junk codes via vanilla lang keys (pemmican-raw-basic, fishchunk-raw don't exist; dropped).
- Implemented AddictPockets, entity wiring (lazy Pockets, SavePockets, GetDrops override, mugging loot into pockets),
  trade system payment/refresh, packets (+AddictStackData nested contract), trader-style dialog, lang keys.
- Build error: `Func<,>` ambiguous (Vintagestory.API.Common.Func vs System.Func) -> used System.Func explicitly.
- Coordinator note (#36 skin uses seraph walk/sprint anims): verified EntityAgent.GetWalkSpeedMultiplier applies
  SprintSpeedMultiplier (2.0) when servercontrols.Sprint for any EntityAgent. Flee now sets Sprint and Walk()
  divides by the multiplier so FleeSpeed stays the net speed. StopMoving/other paths clear Sprint.
- tests/AddictProbe: ran a disposable localhost server (scratchpad dataPath, port 42499, own Mods dir, NOT the
  shared deploy folder). 134 checks passed: all junk codes, jug, stock ranges, payment math, heroin->jugs, tree +
  entity ToBytes/FromBytes persistence, death drops every stack, despawn drops nothing.
  Note: server logs are `Logs/server-main.log` (not .txt) for a --dataPath server.
- Pre-existing (not this task): `recipes/barrel/coca-leaf-to-paste.json` references nonexistent
  `game:aquavitaeportion` (warning at load).
- Docs updated: RUNTIME, DEPENDENCIES, STRUCTURE, MAINTENANCE.
