# Progress

2026-09-24: Read repository guide/maps, fetched master, created a separate worktree, and traced the cocaine countdown to the native dialog/mouse-grab path. Preparing a focused regression probe before changing production code.

2026-09-24: The initial console host needed a runtime dependency resolver for game-bundled cairo-sharp and other GUI interface types. Added resolution from the installed game's root/Lib/Mods. Baseline then reproduced 12 failures (9/21 passed), including cursor release and focus capture. Converted to HudElement, disabled focus/mouse events, closed stale HUDs on death/exit/disposal, and stopped recomposing unchanged text. The identical probe now passes 21/21; git diff --check is clean. Updated runtime/dependency maps and documented rendered-client acceptance.

2026-09-24: Production build and isolated deploy.sh deployment succeeded with zero warnings/errors. Server smoke setup initially hit an unsupported --assetsPath argument (native parser null-reference before mod loading), then an incomplete hand-written config missing default groups. Verified native ServerProgramArgs and regenerated the disposable config with --genconfig; set loopback-only/no advertising and a unique port/save path. No production game installation or shared world was changed. Upstream master remains e994694.

2026-09-24: Final deployed mod reached GameReady/WorldReady on 1.22.7 with all 58 patches applied and no mod errors/exceptions. The pre-existing game:aquavitaeportion recipe warning remains. Final input regression: 21/21 pass; build: zero warnings/errors. Client rendering and physical mouse-look still need the documented in-game acceptance. Work is committed locally; no push, PR or merge is authorized for this task yet.
