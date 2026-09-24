# Findings: overdose roll (#28) and drug strength (#29)

## Requirements
- #28: every dose rolls an overdose chance. Per-drug base plus an increase per recent dose inside a rolling window, reduced by tolerance. Applies to all routes. 10 rapid strong shots with no tolerance should be very likely to overdose, and a single dose only a small risk. Keep the existing overdose outcome.
- #29: morphine's visual effect is far too strong. Rebalance all drugs into clear tiers.

## #28 root cause (evidence from ~/.config/VintagestoryData/Logs/Archive/2026-09-24_10_39_55)
- client-chat.log shows 10 heals of 2.4, 2.4, 2.4, 2.21 ... 0.6 HP between 10:42:55 and 10:44:13. That is 10 heroin injections (24 HP/L * 0.1 L * tolerance multiplier). Healing is skipped while overdosing, so the overdose flag was **never set** during the binge.
- The deployed DLL at that time came from `feat/drug-addict-entity` (it has `/spawnaddict`), which was branched **before** PR #26 (24cbd3c, "Fix overdose dose accounting"). `git merge-base --is-ancestor 24cbd3c de3be8d` returns false. That build still had the old model. It metabolised 1.5 load per 5 s real-time timer tick (about 18 load/min) while each injection added only 5, at one injection every 6 to 8 s. The binge's own tolerance also raised the threshold from 15 to 25, so the load never stayed above the threshold.
- The first 6 shots were taken in **Creative** (`/gamemode 1` only at 10:43:49). Vanilla `EntityPlayer.ShouldReceiveDamage` rejects all non-heal damage in Creative and Spectator, so overdose poison could never show there.
- The model on master (after #26) did cross its threshold after about 4 shots. It was still deterministic, and its outcome was only a slow plus 0.2 HP/s. The user asked for a per-dose random roll instead, so the model was replaced.

## #28 design decisions
| Decision | Reason |
| --- | --- |
| chance = min(0.9, (base + step * recent) * (1 - 0.4 * tol)) | Matches the request. Linear stacking is easy to reason about and tune. |
| Recent = doses of any drug in the last 2 game hours (4 real min) | Switching drugs can't dodge the risk. The window covers a binge but not a normal day's use. |
| Timestamps are a `DoubleArrayAttribute` in entity `Attributes` (server, saved), capped at 32 | Persistent, not synced (the client only needs count and risk), and bounded. |
| Roll uses tolerance *before* this dose's tolerance update | Otherwise a binge's own tolerance protects it (the old model's flaw). |
| Tolerance reduction is at most 30% | Tolerance helps, but it can't make binging safe. |
| Numbers: opium .5%/1.5%, coca 1%/3%, morphine 1.5%/4%, heroin 2%/5% | Single dose ≤2%. 10 rapid heroin shots ≈90% OD (95% at zero tolerance), morphine ≈83%. |
| Outcome kept: slow (0.75-0.6*sev, floor 0.1), heal block, poison above sev 0.35, same HUD keys | The task said to keep the outcome. Damage lowered from 0.2 to 0.1*sev/s because severity now starts high after a binge. A full-severity OD is about 10 HP (near-death, recoverable), and it gets worse if you keep dosing. |
| Severity = base(drug) + 0.05*recent, added onto current severity; recovers 0.5/game hour | Heavier binges give worse overdoses, and re-overdosing stacks. |
| Vessel drink rolls once per 0.1 L | Same unit as a syringe dose. |
| Roll is a `Func<double>` property | The probe can make it deterministic. |
| Legacy `vs-dope-load-*` keys removed on join | Clean migration. |

## #29 findings
- Vanilla caps (decompiled `CollectibleObject`, `BlockLiquidContainerBase`, `BlockMeal`): intoxication `Math.Min(1.1f, …)` and psychedelic `Math.Min(2f, …)`. `PsychedelicPerceptionEffect` intensity = psychedelic / 2, and `DrunkPerceptionEffect` intensity = intoxication.
- Vanilla `EntityBehaviorHunger.detox` lowers both by `0.005 * SpeedOfTime * CalendarSpeedMul / 30` per second. At default (60 * 0.5) that is 0.005/s, about 0.6 per game hour. It runs in every game mode.
- Vanilla references: fly agaric psychedelic 0.4, liberty cap 1.4, blue meanie 2.0. Alcohol has intoxication 3.0/L and spirits 1.5/L.
- The mod clamped at 25. Morphine added **6** intoxication (item and syringe), about 5.5x vanilla's maximum drunkenness, then subtracted it after 5 s, which gave a violent spike. Opium added 3 and coca vitae 1. Heroin added only 0.1 intoxication and 0.15 psychedelic, so the ordering was inverted.
- Coca paste is not consumable (it dries into coca vitae), and morphine solution is only injected (it equals one morphine dose). Neither has its own visual values.

### Before / after (per dose, before the tolerance multiplier)
| Drug | Intox before | Psych before | Intox after | Psych after |
| --- | ---: | ---: | ---: | ---: |
| Coca vitae | 1.0 | 0 | 0.05 | 0.15 |
| Opium | 3.0 | 0 | 0.15 | 0.10 |
| Morphine (item / 0.1 L morphine solution injection) | 6.0 | 0 | 0.25 | 0.20 |
| Heroin (0.1 L syringe or vessel) | 0.1 | 0.15 (tolerance-scaled) | 0.35 | 0.40 |
| Cap | 25 | 25 | 1.1 | 2.0 |

- All four drugs now scale with the tolerance multiplier. Effects fade through vanilla detox, not a 2.5 to 5 s subtraction. `heroin.json` nutritionPropsPerLitre was synced to 3.5 / 4.0 per litre. On join, saved values above the caps are clamped.
- Heal and speed numbers were left unchanged, since the issue is about visual strength.

## Verified 1.22.7 API facts used
- `SyncedTreeAttribute.SetAttribute(string, IAttribute)`, `DoubleArrayAttribute(double[])` with a public `value` field, and the `TreeAttribute` indexer returns null when the key is missing.
- `Entity.Attributes` is saved in `Entity.ToBytes` when `!forClient` (server-only, persistent).
- Default calendar: `currentSpeedOfTime = 60`, `CalendarSpeedMul = 0.5`, so 1 game hour = 120 real seconds.
- `EntityPlayer.ShouldReceiveDamage` blocks non-heal damage in Creative and Spectator.
