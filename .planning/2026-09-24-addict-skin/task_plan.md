# Addict skin (issue #36)

Goal: replace the blocky placeholder drug addict with the vanilla humanoid (seraph) shape and its
animations, wearing a custom gaunt, ragged addict skin.

## Phases
- [x] Research vanilla humanoids (trader, villager, playerbot), seraph shape, texture layout, animation triggers
- [x] Verify how onControls triggers reach the client for a server-steered EntityAgent (decompile)
- [x] Generate skin texture with tools/build_addict_texture.py (+ preview)
- [x] Rewrite `client` section of entities/drugaddict.json (server section untouched)
- [x] Remove old placeholder shape
- [x] Update docs/repo maps
- [x] dotnet build, JSON validation, asset existence checks
- [ ] In-game check by user (see progress.md)

Current phase: done, awaiting in-game check. Next step: user restarts game and runs the steps in progress.md.
