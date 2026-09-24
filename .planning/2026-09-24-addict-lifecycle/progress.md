# Progress

## 2026-09-24
- Read the agent guide, the previous addict planning notes, the entity/trade/spawn code and the logs (no kill audit lines, no attack exceptions).
- Decompiled Entity, EntityAgent, EntityBehaviorHealth/Despawn/DeadDecay, ClientMain.TryAttackEntity, ServerSystemEntitySimulation.HandleEntityInteraction, CollectibleBehaviorAnimationAuthoritative and EntityCubeParticles. Root cause in findings.md.
- #30: reworked `entities/drugaddict.json` and `shapes/entity/drugaddict.json` and changed OnInteract to run the attack path on both sides.
- #31: added the `Leave` state with persistence, `CloseAddictTradePacket` and the `AcceptsTrade` gate. Added lang keys `addict-leaving` and `addict-busy-leaving`.
- Build: 0 errors. Warnings 31 -> 31, all pre-existing. The first nullable warnings from new code were fixed by annotating `EntityPlayer?`/`DamageSource?`.
- Not deployed (the coordinator deploys the combined result).

## In-game verification still needed
See the commit and final report for the steps: /spawnaddict, hit it to death, sell and watch it leave, relog while it leaves.
