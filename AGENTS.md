# vs-dope — agent guide

Vintage Story **1.22.7** code mod (C#, **.NET 10**). Poppy and coca crops, drug processing chains, consumables, addiction/tolerance/overdose, trader integration and a "drug addict" NPC.

- Mod ID `vs-dope`, C# namespace `VsDope`, entry point `src/VsDopeModSystem.cs`
- Game install (API DLLs + vanilla assets): `/opt/vintagestory`
- Game data dir (mods, logs, saves): `~/.config/VintagestoryData`

---

## 0. READ THIS FIRST: your Vintage Story knowledge is out of date

Your training data covers Vintage Story up to about **1.19/1.20**. This project targets **1.22.7**. Between those versions, APIs were renamed, signatures changed, members became `[Obsolete]`, asset folders moved, and the runtime went from .NET 7 to .NET 10.

**Rules:**

1. **Do not write a VS API call from memory.** Before you use a class, method, overload, enum member or JSON property you haven't already seen in this repo, look it up in the local 1.22.7 files (section 1).
2. **Look at vanilla JSON before writing asset JSON.** Find a vanilla file that does the same thing and copy its shape.
3. **Existing code in `src/` is your best example.** It compiles against 1.22.7. Copy its patterns before you invent new ones.
4. **If a lookup contradicts what you remember, the lookup wins.** Record surprising findings in section 4 or in the task's `findings.md`.
5. **Don't guess and loop.** If the build fails on an API member, look it up. Don't try random variants of the name.
6. **Online docs and wiki pages are often from before 1.22.** Treat them as hints and check them against the local files.

---

## 1. How to look things up (1.22.7 ground truth)

Always set this first. The build fails with about 130 misleading errors when it is unset:

```bash
export VINTAGE_STORY=/opt/vintagestory
export PATH="$PATH:$HOME/.dotnet/tools"   # for ilspycmd
```

### Where the code lives

| DLL | Contains |
| --- | --- |
| `$VINTAGE_STORY/VintagestoryAPI.dll` | `Vintagestory.API.*`: Entity, EntityAgent, Item, Block, ItemSlot, GuiDialog, network, events |
| `$VINTAGE_STORY/Mods/VSEssentials.dll` | Entity behaviors, **AI tasks** (`AiTaskBase`, `AiTaskManager`, `EntityBehaviorTaskAI`), pathfinding |
| `$VINTAGE_STORY/Mods/VSSurvivalMod.dll` | `Vintagestory.GameContent`: crops, barrels, liquid containers, traders, most survival blocks/items |
| `$VINTAGE_STORY/VintagestoryLib.dll` | Engine internals (`ServerMain`, `NetworkChannel`). Read it to understand behavior. Avoid depending on it. |
| `$VINTAGE_STORY/Lib/protobuf-net.dll` | **protobuf-net 2.4.0** (network packet serialization) |
| `$VINTAGE_STORY/Lib/0Harmony.dll` | Harmony (runtime patching) if ever needed |

The csproj currently references VintagestoryAPI, VintagestoryLib, VSSurvivalMod and protobuf-net. Add `VSEssentials` (same `HintPath` pattern, `<Private>false</Private>`) if you need AI tasks or behaviors from it.

### Lookup commands

```bash
# XML API docs (fast; summaries + signatures)
grep -n 'EntityAgent\.' $VINTAGE_STORY/VintagestoryAPI.xml | head -50

# Find which DLL/namespace a type is in
for d in $VINTAGE_STORY/VintagestoryAPI.dll $VINTAGE_STORY/Mods/VS*.dll $VINTAGE_STORY/VintagestoryLib.dll; do
  ilspycmd -l cise "$d" 2>/dev/null | grep -i 'AiTaskBase' && echo "  ^ in $d"; done

# Decompile one type: exact 1.22.7 signatures, overrides, and behavior
ilspycmd -t Vintagestory.API.Common.EntityAgent $VINTAGE_STORY/VintagestoryAPI.dll | less
ilspycmd -t Vintagestory.GameContent.BlockBarrel $VINTAGE_STORY/Mods/VSSurvivalMod.dll > /tmp/x.cs

# Vanilla asset examples (entities, recipes, patches, tradelists, dialogue)
ls $VINTAGE_STORY/assets/survival/          # blocktypes itemtypes entities recipes patches config shapes ...
grep -rl '"class": "EntityTrader"' $VINTAGE_STORY/assets/survival/entities
```

Decompiling is usually faster than a build-and-playtest loop. Use it to answer "why doesn't this work" questions too, not just "what's the signature" ones.

---

## 2. Build, deploy, test

```bash
export VINTAGE_STORY=/opt/vintagestory
dotnet build                 # must be 0 errors; fix new warnings you introduced
./deploy.sh                  # builds + copies to ~/.config/VintagestoryData/Mods/vs-dope/
```

- Mods load from `~/.config/VintagestoryData/Mods/vs-dope/`, **not** `ModLoader/`. The folder root must contain `modinfo.json`, `vs-dope.dll` and `assets/vs-dope/`.
- **DLL changes need a full game restart.** Asset-only changes can hot-reload with F3+R, but restart if in doubt.
- Logs: `~/.config/VintagestoryData/Logs/` (`client-main.txt`, `client-debug.txt`, `server-main.txt`). After deploying, grep them for `vs-dope`, `Exception` and `Failed`. **An asset in the wrong folder or with a bad code usually fails silently.** Check that it actually loaded.
- Validate JSON you touched. VS JSON allows comments and unquoted keys, so a strict parser may complain about valid files. Compare with vanilla when unsure.
- Integration tests: `tests/SyringeProbe` is a test-only server mod that asserts against the live registry (see `docs/SYRINGES.md`). Copy that pattern for testable server logic. Never ship it in the mod.
- You can't playtest. When a change needs in-game confirmation, say exactly what the user should do and what they should see (commands, clicks, expected result).

---

## 3. Project map

Read `docs/repo/README.md`, then only the map you need:

| Map | Covers |
| --- | --- |
| `docs/repo/STRUCTURE.md` | tree, entry points, build layout |
| `docs/repo/RUNTIME.md` | C# systems, consumables, addiction/tolerance, client UI |
| `docs/repo/ASSETS.md` | crops, items, shapes, textures, lang |
| `docs/repo/PROCESSING.md` | poppy/coca processing chains, recipe state |
| `docs/repo/DEPENDENCIES.md` | cross-file links, "if you change X also change Y" |
| `docs/repo/MAINTENANCE.md` | known debt, search terms |

The live code wins over the maps. When you add or change a subsystem, asset family, processing chain, persistent attribute key or cross-file dependency, update the relevant map in the same change.

Code layout:

```
src/VsDopeModSystem.cs        registration (Start) + server system wiring (StartServerSide)
src/Items/                    DrugConsumableItem (+ Opium/Morphine/CocaVitae), SyringeItem
src/Systems/                  AddictionSystem, SyringeRecipeSystem, AddictTradeSystem, DrugAddictSpawnSystem
src/Entities/                 EntityDrugAddict (EntityAgent subclass)
src/Network/                  protobuf packet classes
src/Client/                   GuiDialogs / HUD / character tab
assets/vs-dope/               blocktypes itemtypes entities recipes patches statuseffects shapes textures lang
tools/                        Python generators/previewers for crop models
.planning/                    per-task plan/findings/progress notes
```

Poppy chain: seeds → crop → harvest seedpods (knife) → quern → opium → barrel + limewater (24h) → morphine → barrel + alcohol (12h) → morphine solution → barrel + alcohol (6h) → heroin.

---

## 4. Verified 1.22.7 facts (things older knowledge gets wrong)

Each of these was checked by decompiling or by a failed build/playtest in this repo. Add new ones when you find them.

**Assets / JSON**
- Entity type JSON goes in `assets/vs-dope/entities/`, **not** `entitytypes/`. The wrong folder loads nothing and logs no error.
- Texture refs use `"base"` (not `"file"`) with no `.png` extension. Textures must be power-of-two PNGs.
- **Never use `"sides"` in blocktype JSON.** It produces null texture bases that crash in `CollectibleNet.ToPacket`. Use `sideopaque: { "all": false }` / `sidesolid: { "all": false }`.
- Crops use vanilla `cropProps` + class `BlockCrop`. Variant group `"stage"` with string states `"1","2",...`. Codes follow vanilla: `vs-dope:crop-poppy-{stage}`, `vs-dope:seeds-poppy`. The shape texture key must match the blocktype `textures` key (e.g. `#crop` ↔ `"crop"`). Vanilla reference: `$VINTAGE_STORY/assets/survival/blocktypes/plant/crop/`.
- Barrel recipes: `"ingredients"` array, `"sealHours"`, liquids specified with `"litres"`. Vanilla liquids have bare codes: `waterportion`, `limewaterportion`, `alcoholportion` (this **is** Aqua Vitae; there is no `aqua-vitae` or `aquavitaeportion`). Barrel ingredients must be exact multiples of the recipe ratio. A bad code only shows up as `failed to resolve N recipes` in `server-main.log`.
- Liquid itemtypes with `creativeinventoryStacks` need `attributes.handbook.ignoreCreativeInvStacks: true` (like vanilla). Otherwise the handbook page is the filled bucket and the "created by" barrel and distillation sections go missing.
- Our codes are always prefixed `vs-dope:`. Vanilla codes can be bare or `game:`.
- Modify vanilla files (e.g. trader lists) with JSON patches in `assets/vs-dope/patches/`, never by copying the vanilla file.
- Animation keyframe offsets, rotations and stretches must specify all three XYZ axes whenever any axis in that group is present. In 1.22.7, `Animation.lerpKeyFrameElement` dereferences all three nullable values; a partial vector crashes on first playback. Validate custom animations with `Shape.InitForAnimations` followed by `Animation.GenerateAllFrames`, not just JSON parsing (see `tests/MarijuanaProbe/SmokingAnimationChecks.cs`).
- Currency is `game:gear-rusty`.

**C# API**
- Register classes in `Start()` (both sides): `api.RegisterItemClass("vs-dope.x", typeof(X))`, `api.RegisterEntity("vs-dope.x", typeof(X))`. JSON `"class"` uses the same string.
- `Entity.ServerPos` is `[Obsolete]` and is the same object as `Pos`. Use `Pos`.
- `ItemSlot.Itemstack` has a lowercase s. `slot.TakeOut(n)` lives on the slot.
- Attributes use `HasAttribute(key)` (not `HasKey`).
- `EntityPlayer` does **not** implement `IServerPlayer`, so `entityPlayer as IServerPlayer` is always null and the compiler doesn't warn you. Use `sapi.World.PlayerByUid(entityPlayer.PlayerUID) as IServerPlayer`.
- `Entity.EntityId` is `long`.
- `Entity.OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)` is the current override.
- `ICoreServerAPI.RegisterCommand` is `[Obsolete]`. Use `api.ChatCommands.Create(name).WithDescription(..).RequiresPrivilege(..).RequiresPlayer().WithArgs(api.ChatCommands.Parsers.OptionalIntRange(..)).HandleWith(args => TextCommandResult.Success(..))`. Parsed args are `args[i]`; the player is `args.Caller.Player`.
- An entity's display name is lang key `item-creature-<code>` (and `item-dead-creature-<code>` once dead), read by `Entity.GetName()`. Keys like `entity-type-<code>` are never read.
- `Func<,>` is ambiguous between `System` and `Vintagestory.API.Common`. Write `System.Func<...>`.
- The spawn pipeline calls `entity.Initialize()` **after** your pre-spawn setup. Persist state into `WatchedAttributes` before spawning and don't let `Initialize` overwrite state that's already bound.
- AI tasks **are** moddable: `AiTaskBase` lives in `VSEssentials.dll` (see section 1). An older note in `.planning/` says otherwise; that note is wrong.
- Real trader classes (`EntityTrader`, `GuiDialogTrading`) are in game content and not meant for reuse. Our trade UI is a custom `GuiDialog` (see `src/Client/AddictTradeUiSystem.cs`).

**Networking**
- Every packet class needs `[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]`. protobuf-net 2.4.0 throws "no contract can be inferred" on plain POCOs, and it only fails at runtime.
- Setup: `api.Network.RegisterChannel("vs-dope.x").RegisterMessageType<T>()` on **both** sides, then `SetMessageHandler<T>`.
- Server send: `channel.SendPacket(packet, serverPlayer)`. The packet comes **first**, then the player(s).

**GUI**
- `EnumButtonStyle` = `None, MainMenu, Normal, Small`.
- `ActionConsumable` = `bool ()`.
- `CairoFont.WithFontSize(float)`.
- `EnumChatType` has no `Error`. Use `CommandError` or `Notification`.
- `GuiDialog.TryOpen()` registers itself. Copy the Compose/TryOpen pattern in `src/Client/CocaVitaeEffectHudSystem.cs`.
- `InventoryGeneric` ids must contain a `-` (the constructor splits on it).

**Side awareness**
- Game logic, inventory changes, spawning and damage are **server**. GUI and HUD are **client**. Anything that has to reach the other side goes through a packet or `WatchedAttributes`.
- Guard side-specific code: `if (api.Side != EnumAppSide.Server) return;` or `api is ICoreServerAPI sapi`.

---

## 5. Conventions

- Match the surrounding code style: file-scoped namespaces, nullable enabled, small focused systems.
- Every new player-visible string gets a lang key in `assets/vs-dope/lang/en.json` (keys there are unprefixed; in C# use `Lang.Get("vs-dope:<key>")`). Don't hard-code English in C#.
- Keep balance numbers (prices, durations, chances) as named constants or JSON attributes, not magic numbers scattered through the code.
- Don't touch `bin/`, `obj/` or `generated-images/`. Never commit build output.
- Before renaming a code, class name, attribute key or lang key, grep the whole repo (`src/`, `assets/`, `tests/`, `docs/`) for it.

## 6. Planning notes (`.planning/`)

For any multi-step task, create `.planning/YYYY-MM-DD-<slug>/` containing:
- `task_plan.md`: goal, phases with checkboxes, current phase, next step
- `findings.md`: requirements, user answers, API research, decisions (with a reason for each)
- `progress.md`: dated session log, errors hit and how you fixed them

Write the folder name into `.planning/.active_plan`. Update `progress.md` as you go, not only at the end. When you resume a task, read these first. They record API facts that were already verified.

## 7. Git workflow

- **One fresh branch per task** off an up-to-date `master` (this repo uses `master`, not `main`), named `<type>/<short-slug>` (`feat/…`, `fix/…`, `docs/…`).
- **Always work in a worktree.** Every task gets its own worktree at `../vs-dope-<slug>`; do all edits, builds and deploys from there, never from the main checkout:
  ```bash
  git fetch origin && git switch master && git pull
  git worktree add ../vs-dope-<slug> -b <type>/<slug>
  cd ../vs-dope-<slug>
  # when merged: git worktree remove ../vs-dope-<slug>
  ```
- **Commit verified work** before you finish. Stage only the files you meant to change.
- **Ask before pushing or opening a PR.** Never force-push.

## 8. Definition of done

1. `dotnet build` passes with 0 errors (with `VINTAGE_STORY` set).
2. Every new API usage was checked against the 1.22.7 DLLs or XML, not memory.
3. New assets follow a vanilla example and new strings have lang keys.
4. `docs/repo/` maps and `.planning/` notes are updated.
5. Deployed with `./deploy.sh`, and the logs show no new errors from `vs-dope`.
6. You gave the user clear in-game steps for anything you couldn't verify yourself.
