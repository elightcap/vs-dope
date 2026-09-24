# Requirements and decisions
- User: nine marijuana stages matching coca/poppy; Marijuana buds with inventory/ground/hand model; bud + parchment -> Joint.
- Hold-to-smoke pose and drag sound; Stoned lasts 2 calendar hours, -20% walkspeed, heals 0.5 HP/game minute. Optional bloodshot eyes.
- One bud + one parchment -> one joint. Reuse refreshes expiry, never stacks rates. New effect is separate from tolerance/overdose roll (user requested fixed values).
- Current upstream starts at 9c4326da46941ea436e3bd17c56c6d3f6f33afe7. Separate branch feat/marijuana in worktree.
- Local 1.22.7 runtime is /workspace/scratch/25ddf310c8d1/vs-runtime; .NET SDK in adjacent dotnet folder. /opt/vintagestory is absent.

- User steering: smoking must require FIVE seconds (supersedes initial 3s implementation). Animation 150 frames; inhalation begins after .9s and lasts 3.7s.

## Verified 1.22.7 APIs / assets
- Parchment is `game:paper-parchment`; vanilla grid `recipes/grid/parchment.json`.
- Player uses `seraph-faceless`, not just `seraph`. Both smoking shape patches are additive; player animation metadata enables the automatic `-fp` variant.
- `EntityBehaviorExtraSkinnable` uses `EntityBehaviorTexturedClothing.OnReloadSkin`; the latter lives in VSEssentials. Public `doReloadShapeAndSkin` + `Entity.MarkShapeModified()` recomposes skin. Vanilla eyecolor writes at 57,0; sclera UV and iris UV are separate. Custom atlas layouts skipped.
- `SyncedTreeAttribute.RemoveAttribute` dirties the whole tree even for missing keys: clear only when the expiry exists. `UnregisterListener(Action)` is the supported cleanup API.
- The effect tick must exclude connecting players until PlayerNowPlaying resets the cursor, otherwise offline healing could accrue during login.
- Server `PlaySoundAt(AssetLocation, Entity, IPlayer, bool, float range, float volume)` used once; no duplicate owner playback.
- A live 1.22.7 dedicated server loaded all assets and passed the initial 53 checks. Existing unrelated coca/aquavitaeportion warning recorded. No new mod errors.
