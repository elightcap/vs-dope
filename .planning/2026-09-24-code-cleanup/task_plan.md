# Code cleanup (old APIs, dead code)

Goal: remove obsolete API usage, dead code and orphaned assets; move hard-coded English to lang. No behavior change except the addict name lang-key fix.

- [x] Audit src/, tests/, assets (warnings baseline: 31, 2 obsolete-API)
- [x] Obsolete APIs: RegisterCommand -> ChatCommands, ServerPos -> Pos
- [x] Dead code: unused AddictionSystem members, TolKey alias, startup registry dump, unused params/locals
- [x] Nullable warnings
- [x] Hard-coded English -> lang keys (trade UI/server msgs, character tab, coca HUD, spawn command)
- [x] Lang: duplicate overdose-warning, unused keys, fix entity name keys
- [x] Orphaned assets: opium-pile shape, coca v3 textures, coca_atlas
- [x] Stray empty `!` file at repo root
- [x] Build (main + test probes), deploy, check logs, update docs/repo maps
