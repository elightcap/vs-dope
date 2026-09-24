# Addict lifecycle: #30 (addicts don't die) and #31 (addict doesn't leave after purchase)

Branch `fix/addict-lifecycle`, worktree `../vs-dope-addict-lifecycle`.

## Goal
- #30: addicts take visible damage, die at a sensible HP, play a death animation, leave a corpse that decays.
- #31: after a completed sale with no overdose, the addict walks away and despawns once out of sight, with a timeout fallback. The leaving state persists across reloads. Addicts that never trade are cleaned up as before.

## Phases
- [x] 1. Research: decompile Entity/EntityAgent/EntityBehaviorHealth/Despawn/DeadDecay, client attack path, vanilla trader/hare JSON
- [x] 2. #30: entity JSON (health 15, deaddecay, client textures + animations, deadHitboxSize), shape hierarchy + hurt/die anims, OnInteract attack on both sides
- [x] 3. #31: `Leave` state (linger -> walk -> despawn when unseen / timeout), persistence, CloseAddictTradePacket, AcceptsTrade gate
- [x] 4. Build 0 errors, no new warnings (31 before, 31 after)
- [x] 5. Docs maps (RUNTIME.md, DEPENDENCIES.md) + planning notes
- [x] 6. Commit (no push, no deploy: the coordinator deploys)

## Current phase
Done. The user still has to test in game (see progress.md).
