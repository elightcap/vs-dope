# Progress

## 2026-09-24
- Worktree `../vs-dope-addict-skin`, branch `feat/addict-skin` off origin/master.
- Surveyed vanilla humanoid entities/shapes; picked `game:entity/humanoid/seraph` (see findings).
- Decompiled EntityAgent/EntityControls + VintagestoryLib position packet to confirm Move trigger path.
- Wrote `tools/build_addict_texture.py`; generated `textures/entity/drugaddict.png` (64x152) and `docs/previews/drugaddict.png`. First pass: hair texture looked grey (name "blackolive" is a light grey) -> switched to `rust3`; toned down red eye flecks that read as red cheeks; more grime on shins.
- Rewrote drugaddict.json `client`; hitbox 1.85 / eyeHeight 1.7. Removed `shapes/entity/drugaddict.json` (no other references).
- Updated docs/repo ASSETS, DEPENDENCIES, STRUCTURE.
- Not deployed (coordinator deploys).

## In-game check (user)
1. Full game restart after deploy. `/spawnaddict` (or wait for a natural spawn).
2. Expect a player-sized seraph with sallow grey-yellow skin, greasy dark hair, dark eye rings, dirty torn off-white shirt with short ragged sleeves, brown patched trousers torn at the shin, rag-wrapped feet.
3. While it walks toward you: legs/arms swing (walk anim). When it stops next to you: shivering hunched idle (coldidle). Hit it: hurt flinch. Kill it: falls over (die), corpse decays with particles.
4. Check `client-main.txt` / `client-debug.txt` for `drugaddict`, `seraph`, `Exception`, `not found`.
