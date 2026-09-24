# Findings

## Requirements
- Issue #36: addict needs a real skin, walk/idle animations.
- User choice: reuse vanilla humanoid shape + animations, custom gaunt/ragged look.
- Only edit `client` section (+ top-level sizes) of drugaddict.json; server section and EntityDrugAddict.cs belong to another agent (#38/#40).

## Research (1.22.7, verified locally)
- Traders/villagers (`survival/entities/humanoid/trader-*.json`, `villager.json`) use classes `EntityTrader`/`EntityVillager` (EntityDressedHumanoid): clothing is attached by that class from `outfitConfigFileName` configs. Our class is a plain `EntityAgent`, so that outfit system does not apply. Decision: do not use trader/villager shapes (their body texture is skin only; clothes would be missing).
- `survival/entities/humanoid/playerbot.json` is the closest match: plain seraph body with control-triggered animations. Copied its animation block shape (walk/sprint/idle/swim/hurt/die).
- `game/shapes/entity/humanoid/seraph.json`: textureWidth/Height 32x76, `textureSizes` seraph [32,76], hair [48,48]; texture keys `seraph` and `hair`; eyes, brows, mouth, hair are built in (unlike `seraph-faceless`, which needs skinparts). Has animations walk, sprint, idle1, coldidle, newjump, swim, swimidle, hurt, die.
- Vanilla seraph body skins are 64x152 (2 px per UV unit) - not power-of-two. Our skin keeps 64x152 so pixels map 1:1. Reason: any other size would stretch the UV mapping.
- The face is the head's west face (eyes sit at the head's min-x). Face feature elements sample x 56-63, y 0-27 px: eyes rows 0-3, shine 8-11, eyelids 12-15, lips 16-19, brows 20-27.
- Hair texture names don't match their colours; `rust3` averages (65,55,34) = greasy dark brown.
- Animation triggering (decompiled `EntityAgent.OnGameTick`, VintagestoryAPI): on the client, `CurrentControls` is computed from `servercontrols` (TriesToMove => Move, +Sprint => SprintMode, Dead when !Alive). On the server `servercontrols == controls` (EntityAgent.Initialize). Server position packets carry `entityAgent.Controls.ToInt()` and the client does `ServerControls.FromInt(packet.Controls)` (VintagestoryLib). So `EntityDrugAddict.Walk()` setting `Controls.Forward = true` triggers the `Move` walk animation. No C# change needed.
- `Controls.Sprint` would trigger SprintMode (run anim) but `GetWalkSpeedMultiplier` also multiplies speed by `GlobalConstants.SprintSpeedMultiplier` when `servercontrols.Sprint` is set. Not changed; noted for the movement-speed owner.
- `mulWithWalkSpeed` multiplies anim speed by the entity's `walkspeed` stat (1 for the addict), not by WalkVector.
- `EntityClientProperties.PitchStep` exists (default true); playerbot sets `pitchStep: false` for the humanoid body. Copied.

## Decisions
- Idle anim = `coldidle` (full-body shiver) as defaultAnim: reads as withdrawal. One-line swap to `idle1` if it looks wrong.
- Hitbox 0.6 x 1.85 and eyeHeight 1.7 to match the seraph body (playerbot values).
- Texture generated from vanilla `skin18` (pale grey-green) recoloured sallow, painted per UV rect read from seraph.json at run time.
- CLAUDE.md is gitignored in this repo, so the two new verified facts (entity skin aspect exception; onControls <- synced Controls) are recorded here and in docs/repo/ASSETS.md + DEPENDENCIES.md instead.

## 2026-09-24 fix: face and hair on the ground
- In-game the addict's face and hair rendered at its feet. `seraph.json` keeps `eyesroot` and `Hair` as top-level elements with `stepParentName: "Head"`. Step-parenting only happens in `Shape.StepParentShape`, which a plain entity calls only for `client.shape.overlays` (`Entity.OnTesselation`, decompiled). Unresolved, they sit at the model origin.
- Fix: base `game:entity/humanoid/seraph-faceless` + overlays `seraphskinparts/face/tired` and `seraphskinparts/hair-base/messy2` (a hair shape without `#null` faces). Overlay texture keys already in `client.textures` (`seraph`, `hair`) keep our textures; `TextureSizes` merge per key, so hair keeps 48x48.
