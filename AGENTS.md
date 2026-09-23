# vs-dope

Vintage Story mod (1.22.7): poppy cultivation and drug processing chain with addiction mechanics.

## Repository navigation docs

For future development/agent work, use the focused maps under `docs/repo/` before editing a subsystem:

- `docs/repo/README.md` — navigation index and which map to read.
- `docs/repo/STRUCTURE.md` — repository tree, entry points, build/layout.
- `docs/repo/RUNTIME.md` — C# runtime, consumables, addiction/tolerance, client UI.
- `docs/repo/ASSETS.md` — crops, items, shapes, textures, localization.
- `docs/repo/PROCESSING.md` — implemented/intended game processing chains and recipe state.
- `docs/repo/DEPENDENCIES.md` — cross-file dependency maps and common-change lookup.
- `docs/repo/MAINTENANCE.md` — known debt, safe navigation workflow, search index.

Read only the relevant maps, then verify against the current live tree and implementation. Update the appropriate map whenever a change adds or alters a subsystem, asset family, processing chain, persistent key, or important cross-file dependency.

## Git Workflow (always follow)

- **Fresh branch per task.** Never work directly on `main`/`master`. At the start of each task create a new branch off an up-to-date base named `<type>/<short-slug>` (e.g. `feat/trader-drug-lists`, `fix/addiction-dup-members`).
- **Use git worktrees.** Give each concurrent task its own worktree so branches don't collide and the build stays isolated:
  ```bash
  git fetch origin && git switch main && git pull
  git worktree add ../vs-dope-<short-slug> -b <type>/<short-slug>
  cd ../vs-dope-<short-slug>   # do all edits/builds here
  ```
  Remove the worktree when done: `git worktree remove ../vs-dope-<short-slug>`.
- **Commit always.** Commit completed, verified work before wrapping up — stage only intended files (no build artifacts / secrets). Do not leave changes uncommitted.
- **Ask before pushing.** When finished, ask the user whether to push the branch to GitHub and open a new PR/branch. Never force-push or push without that confirmation.

## Processing Chain

poppy seeds → grow → harvest seedpods + seeds → quern grind pods → opium → barrel (+ limewater portion, 24h) → morphine → barrel (+ alcohol portion, 12h) → morphine solution → barrel (+ alcohol portion, 6h) → heroin

Uses vanilla liquids: `limewaterportion` (made from `lime` + `waterportion` in vanilla recipe), `alcoholportion`.

## Build

```bash
export VINTAGE_STORY=/path/to/vintagestory/install   # dir containing VintagestoryAPI.dll
dotnet build
```

Test in-game: mods load from `~/.config/VintagestoryData/Mods/<modid>/` (NOT `ModLoader/`). Layout must be `modinfo.json` + `<modid>.dll` + `assets/<modid>/` at that folder root.

```bash
DEST=~/.config/VintagestoryData/Mods/vs-dope
rm -rf "$DEST" && mkdir -p "$DEST/assets"
cp modinfo.json "$DEST/"
cp bin/Debug/vs-dope.dll "$DEST/"
cp -r assets/vs-dope "$DEST/assets/"
```

Restart game for DLL changes (assets hot-reload with F3+R, code does not). Verify load path in `Logs/client-debug.txt`.

## Structure

- `modinfo.json` — targets >=1.22.7, side: Both
- `src/Systems/AddictionSystem.cs` — player stat tracking via `player.Attributes`, ticked on hour change
- `src/Items/DrugConsumableItem.cs` — base class + Opium/Morphine/Heroin variants (heal + slow + drunk effect + addiction recording)
- `assets/vs-dope/blocktypes/poppy-plant.json` — 8 growth stages, drops seeds + seedpods with knife
- `assets/vs-dope/statuseffects/` — withdrawal and doped effects

## Conventions

- Mod ID: `vs-dope`, namespace: `VsDope`
- Item/block codes prefixed `vs-dope:` in all JSON references
- Custom item classes registered via `api.RegisterItemClass("modid.classname", typeof(Class))` in `ModSystem.Start()`; referenced in JSON as `"class": "vs-dope.opiumitem"` etc.
- Barrel recipes use vanilla format: `"ingredients"` (plural array), `"sealHours"`, liquids specified with `"litres"` property. No namespace prefix needed for vanilla items.
- Crop follows vanilla naming convention: block code `"crop"` + variant type `"poppy"`, seed item code `"seeds"` + variant type `"poppy"` → full codes `vs-dope:crop-poppy-{stage}` and `vs-dope:seeds-poppy`

## Gotchas

- Textures must be power-of-two PNG — placeholder solid-color PNGs created; replace with real art
- `VINTAGE_STORY` env var required for build (not committed); set to your Steam or manual install path
- Vanilla liquid items use bare codes without prefix in recipes: `limewaterportion`, `alcoholportion`, `waterportion`. No `aqua-vitae` exists.
- The quern grinding uses `"quernProps"` attribute on seedpod; verify this matches vanilla VS 1.22 quern API (may need adjustment after testing)
- Addiction persists via `player.Attributes` (survives relog), but no serialization of withdrawal state across server restarts yet
- **Never use `"sides"` in blocktype JSON** — it creates per-side CompositeTexture objects with null Base that crash during network encoding (`CollectibleNet.ToPacket`). Use `sideopaque: { "all": false }` and `sidesolid: { "all": false }` instead. Vanilla reference: `/opt/vintagestory/assets/survival/blocktypes/plant/crop/`
- Crop blocks should use vanilla's native `cropProps` + `BlockCrop` class (not SeededPlant behavior). Variant group must be `"stage"` with string states (`"1","2",...`). Shape texture key must match blocktype textures dict key (e.g. shape uses `#crop`, blocktype has `{ "crop": { ... } }`)
- Texture references in JSON use `"base"` not `"file"`, and omit `.png` extension
