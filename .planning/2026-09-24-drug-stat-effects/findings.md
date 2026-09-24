# Findings

## User decisions (2026-09-24)
- Opioid downside is ranged accuracy only. "Animals detect you from farther away" was dropped (see below).
- Heroin gets the opioid profile as the strongest opioid.
- Durations: opium 1 h, morphine 1.5 h, heroin 2 h, coca 1 h + 1 h crash, marijuana 2 h (existing).

## 1.22.7 API (decompiled)
- Player stats are registered in `EntityPlayer` (VintagestoryAPI): miningSpeedMul, hungerrate, healingeffectivness, rangedWeaponsAcc, animalSeekingRange, walkspeed. The default blend is WeightedSum: base 1 plus the modifiers. `Stats.Set(cat, code, value, persistent)`; a persistent value is saved with the entity.
- animalSeekingRange: `AiTaskBaseTargetable.CanSensePlayer` / `GetDetectionRangeMultiplier` (VSEssentials). The candidate search radius is the task's own seekingRange (`partitionUtil.GetNearestEntity(pos, seekingRange, …)`), so a multiplier above 1 is never reached. Only values below 1 work.
- healingeffectivness: read by `ItemPoultice` (VSSurvivalMod) and in the bandage application time. It does not scale direct `ReceiveDamage(Heal)`.
- No damage-reduction stat exists. `EntityBehaviorHealth.onDamaged` (`float (float dmg, DamageSource src)`) runs before heal and damage are applied, heals included, so the handler skips `Heal`.
- `EntityBehaviorHunger.ConsumeSaturation(float)` is public virtual.
- `Entity.GetBehavior<T>()` is virtual. Probe entities have no Properties, so the probe overrides it.

## Decisions
- One system with a profile table, so numbers stay as named data (a CLAUDE.md convention) and every dose route shares one lifecycle.
- The multiplier at dose time is stored in Attributes, so relog and crash use the same strength.
- The crash starts at the high's expiry, not at tick time, so offline time doesn't extend it.
- Mitigation takes the strongest opioid, not the sum. It skips Internal damage so opioids can't blunt their own overdose.
- Marijuana stats stay in StonedSystem's lifecycle (single owner). Marijuana has no tolerance, so the multiplier is 1.
