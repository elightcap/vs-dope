# Practical drug effects (#55)

These are game balance values, applied server-side and reduced by the product's
pre-dose tolerance multiplier. First two uses per game day add no tolerance.

| Product | Duration (game hours) | Base benefits | Base costs |
| --- | ---: | --- | --- |
| Coca Vitae | 1 | +25% stone/ore mining; -20% hunger rate | Afterwards: 150 satiety once and -20% speed for 0.5 game hours |
| Opium | 0.5 | +15% bandage/poultice healing; -10% physical attack damage | -15% ranged accuracy stat; +15% creature detection factor |
| Morphine, oral or injected | 1 | +30% bandage/poultice healing; -20% physical attack damage | -30% ranged accuracy stat; +25% creature detection factor |
| Cannabis | 2 | -25% creature detection factor; existing 0.5 HP/game-minute healing | +25% hunger rate; existing -20% speed |

Coca's +100% speed for one game hour and heroin's -50% speed for one game hour
remain unchanged at zero tolerance. Opium/morphine retain their existing brief
movement penalties (2.5/5 real seconds); the new support effects use the longer
calendar durations above. Cannabis now uses its own tolerance counter for all
effect magnitudes, without contributing to opiate addiction or overdose rolls.

Repeated doses refresh one timer per product; they do not stack that product's
modifiers. Different products combine their stat modifiers. Physical damage
protection takes the strongest active opiate protection, never their sum.
Healing, internal poison (including overdose), starvation, falling, fire, and
other environmental damage are not reduced by this protection.

## Native 1.22.7 behavior

- `miningSpeedMul` is read by `CollectibleObject.GetMiningSpeed` for stone/ore.
- `hungerrate` is read by `EntityBehaviorHunger.ReduceSaturation`.
- `healingeffectivness` (vanilla spelling) changes application time in the live
  `CollectibleBehaviorHealingItem`; the older `ItemPoultice` uses it for amount.
  The health hook also multiplies the opiate bonus into internal scheduled
  healing once (live bandages/poultices), not again on delivered healing ticks.
  It does not multiply instant drug healing or duplicate legacy poultice scaling.
- `rangedWeaponsAcc` is read by `BaseAimingAccuracy`; the percentage modifies the
  stat used in its nonlinear accuracy calculation, not final hit probability.
- `animalSeekingRange` is read by both old and new vanilla targetable AI. New AI
  respects `SeekingRangeAffectedByPlayerStat`; light, sneaking, hostility, and
  each task's initial maximum search radius still apply. A positive modifier
  can offset stealth/light reductions but does **not** expand that outer radius.
  This is not invisibility or a promise that every custom mob obeys the stat.
- A server-only Harmony prefix on `EntityBehaviorHealth.OnEntityReceiveDamage`
  supplies the otherwise unavailable general physical-attack protection. It
  filters damage protection to players, Entity/Player sources and blunt/slashing/piercing attacks.
  Scheduled damage is reduced on delivery, not both at scheduling and delivery.

## State and lifecycle

`DrugToolEffects` runs every 250 ms on fully playing server players. Watched
`vs-dope-tool-<product>-expires-gamehour` and `-strength` record the active dose.
Named stat sources use `vs-dope-tool-<product>`. Reconnection restores the saved
strength and original expiry, including Coca Vitae's established speed/HUD key.

The coca crash uses `vs-dope-coca-crash-expires-gamehour`, `-strength`, and the
`vs-dope-coca-crash` walkspeed source. Active expiry is removed before charging
satiety, so later ticks/logins cannot charge the same dose again. Satiety floors
at zero and does not alter nutrition bars. Offline expiry settles the single
cost on return, while the crash countdown remains tied to the original expiry:
it does not restart after a long absence. Redosing does not remove an active
crash. Death/respawn removes these records without a crash charge.

`StonedSystem` owns cannabis's hunger/detection/movement sources and watched
`vs-dope-stoned-strength`. It settles healing at the old strength before a
refresh. Existing saved Stoned effects without strength default to 1. Its
existing reconnect behavior still prevents offline healing. All cleanup removes
only this mod's sources and preserves other mods' stat modifiers.

## Validation

`tests/OverdoseProbe/ToolEffectChecks.cs` extends the existing server probe. It
checks actual native pickaxe/poultice consumption and the installed native health
hook, plus refresh, tolerance, calendar expiry, crash cost, reconnect, mixed
opiates, syringe routing, cannabis counters, death, and unrelated stat sources.
Player/network endpoints are test doubles; this is not a graphical playtest.
Run alongside `tests/CannabisProbe` on a disposable server and inspect both
summary lines and error logs. Never ship either probe.

Client acceptance after a full restart:

1. In survival, take Coca Vitae and mine the same stone with the same pickaxe;
   verify faster mining and the one-hour HUD. At expiry, verify the satiety drop
   and 30-game-minute crash countdown, then normal movement after the crash.
2. Take opium/morphine while injured, use the same poultice, and compare healing.
   Check reduced physical attack damage and worse ranged aiming. Poison and
   falling should retain their normal damage. Repeat with a morphine syringe.
3. Smoke a joint for five seconds. Check hunger, movement, healing, and the HUD's
   tolerance-adjusted numbers. Approach the same unalerted hostile creature in
   the same light; detection should happen later where its vanilla AI uses the stat.
4. Relog during each effect and crash, then test natural expiry and death.
   No benefit/penalty should remain afterward. Inspect client/server logs.
