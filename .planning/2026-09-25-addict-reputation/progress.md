# Progress

## 2026-09-25
- Worktree `../vs-dope-addict-reputation`, branch `feat/addict-reputation`.
- Researched save data, structures, texture alternates and nametag APIs (see findings).
- Implemented AddictLedger (data + rules), AddictReputationSystem (save/load, daily pass, rumours, /addicts), entity/trade/spawn integration, decline textures (stages 1-3 via texture alternates), nametag, lang keys.
- Build: one CS8625 on `SaveGame.GetData<T>(key, null)`; switched to raw bytes + SerializerUtil.
- Probe run 1 (disposable server, port 42499): FAIL "roundtrip keeps deaths and standing". Cause: protobuf-net omits values equal to the default and keeps the field initializer, so `Alive = false` loaded as `true`. Fixed with `[DefaultValue]` on every non-zero initializer; added a sentinel roundtrip check. Recorded in AGENTS.md §4.
- Probe run 2 failed to bind: previous server still running. Run 3: ADDICT TEST SUMMARY 231 checks passed (128 pockets + 86 ledger + entity binding, texture index, nametag, stale copy, legacy adoption, death once). No vs-dope warnings/errors in the log.
- Texture: base drugaddict.png byte-identical after refactor; eye-ring strength reduced after preview (stage 3 read as a mask).
- Deployed with ./deploy.sh. Client-side rendering (alternates, nametag, window title) not verifiable here: in-game steps given to the user.
- User: nobody can afford heroin. Simulated starting wealth: median 21 gears-equivalent, only ~11% could pay 30 for 1 L (8% at trusted x1.35). Heroin now sells per 0.1 L dose at 3 (same per-litre value); decline exposure 0.4/dose. User: 3 too cheap -> 9 per dose (97% afford one, ~2 per visit; regular 11, trusted 12). AddictPockets.AddLiquidPortions added. Probe: 233 checks passed.
