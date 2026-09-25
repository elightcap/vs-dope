# Reusable bong

Vintage Story 1.22.7 item addition. The empty item is named **Bong** and the filled item **Loaded up Bong**. Both have a stack limit of one.

## Recipes and use

Craft one Bong with two `game:clearquartz`, in these exact 3x3 positions:

| Left | Middle | Right |
| --- | --- | --- |
| Empty | Empty | Empty |
| Empty | Clear quartz | Empty |
| Empty | Clear quartz | Empty |

Combine **one Bong + one Cannabis buds** anywhere in a crafting grid to produce **one Loaded up Bong**. Loading consumes those two input items; it does not leave an extra empty bong behind. No water ingredient or refill is required.

Hold right-click for **five seconds** to smoke. Dedicated first-/third-person `vsdope-bong` animations raise the vessel to the mouth and lower it before completion, and a distinct bubbling/inhalation sound starts after 0.9 seconds. Completing use replaces the loaded item with **one empty Bong in the same inventory slot**, including with a full inventory. A completed use also broadcasts one expanding, fading smoke puff from the mouth in the look direction, visible to the smoker and nearby players. The returned bong can immediately be loaded again. Releasing early, cancelling, dying or changing the held item grants no effect and does not consume the load. Repeated stop callbacks cannot return extra vessels.

The effect is the same **Stoned** effect as a joint: **two in-game hours**, **-20% base movement speed**, **0.5 HP per in-game minute**, **+25% hunger**, and **-25% creature detection factor**. Joints and bongs share cannabis-specific tolerance, which scales all these magnitudes. Subsequent uses refresh the expiry without stacking the rate or speed penalty. Bloodshot eyes and the Stoned HUD use the existing shared systems. Balance changes remain shared; the taller bong has its own arm/wrist pose. See `DRUG_TOOLS.md` for detection limits and tolerance checks.

## Implementation and assets

- `Items/BongItem.cs` is registered as `vs-dope.bong`, with codes `bong-empty` and `bong-loaded`. It uses temporary per-stack use/audio flags and server-side inventory/effect changes. Non-stackable variants permit direct slot replacement, without an inventory insertion or drop fallback.
- Held transforms anchor neck point `(6.5, 7, 8)/16` at the native hand attachment: origin is that grip, scale is 0.65 and translation is `-origin/scale`. GUI X rotation is 165 degrees because item icons do not receive the automatic block flip. Ground rotation explicitly overrides the item default of 90 degrees.
- `recipes/grid/bong-empty.json` uses a full 3x3 shaped pattern `___,_Q_,_Q_`. `bong-loaded.json` is shapeless. `itemtypes/bong.json` supplies variant shapes, texture aliases and GUI/hand/ground transforms.
- `shapes/item/bong-{empty,loaded}.json` are portable native Model Creator shapes: a stepped glass chamber, thick base, narrow hollow neck, open mouthpiece and attached downstem/bowl. The loaded variant adds a green bud cluster using the existing cannabis atlas.
- `textures/item/bong-glass.png` is a 64x64 RGBA generated glass texture. Logical UV dimensions are 64x64. Quiet glass UV `[43,41,45,43]` and rim UV `[35,52,37,54]` avoid stretching the full tile over narrow faces. Glass elements use render pass 3, following vanilla `shapes/item/clutter/art/bottle.json`.
- `tools/build_bong_models.py` regenerates item/models and `patches/bong-player.json` deterministically. `tools/preview_bong_models.py` renders their actual geometry and textures with approximate alpha blending to `docs/previews/bong-items.png`; it is a geometry-only software preview, not a game screenshot or a check of inventory/held transforms.
- `sounds/player/bong-bubbles.ogg` is original synthesized resonant bubbling under soft inhalation noise. `tools/build_bong_audio.py` generates 3.7 seconds of mono 22,050 Hz Vorbis from a fixed seed (numpy/scipy/ffmpeg), without recordings or external samples. Server broadcast is guarded to play once per held use.

## Verification

`tests/CannabisProbe/BongChecks.cs` extends the existing test-only live-server mod. It tests real recipe matching and input consumption, all 36 pairs of quartz positions, all 72 distinct loading positions, rejected extra/missing ingredients, empty/early/cancel/death/switched-item/duplicate-stop safety, and three complete load/smoke/reload cycles in a full native inventory. It checks that exactly one bud is consumed each cycle, one empty bong returns in-place, Stoned refreshes without stacking, and one game minute heals 0.5 HP. Models/textures and the bubbling sound must resolve. The native `SmokingAnimationChecks` runs joint and bong animations against both patched Seraph shapes and both camera variants. `BongPresentationChecks` uses the actual `ClientAnimator`, vanilla body/held idles and native GUI/held matrix order: icon vertical orientation, grip at the hand attachment, mouthpiece near the Seraph mouth, and lowering before exhale. Smoke checks exercise actual completion/cancellation/duplicate callbacks and the native particle packet serialization.

Build/deploy instructions are in `docs/CANNABIS.md`. Never distribute the test probe with the mod. The server's existing coca recipe warning and duplicate `Eyes` attachment-point warning in the full vanilla Seraph are unrelated.

Result on 1.22.7 after the pose/exhale fix: **157 checks passed**. Main mod and probe build with zero warnings/errors; deployment succeeds and all **64 JSON patches** apply without errors. Native blended poses place the mouthpiece within **0.012 blocks** of the mouth in both camera variants; the regression limit is 0.04 blocks. Generator output is deterministic. A software skeleton/geometry study was inspected, but no graphical game client was available; the acceptance checks below remain necessary.

Client acceptance (requires a rendered game):

1. Restart with the updated DLL/assets. In creative, inspect Bong and Loaded up Bong in inventory, in hand and dropped on the ground. Both icons should be upright, the neck should sit in the hand, and the dropped vessel should stand upright. Check translucent glass and the visible loaded bowl.
2. Craft with quartz in centre and bottom-centre. Move either quartz elsewhere and verify the output disappears. Load the bong with one bud in different grid positions.
3. Smoke for five seconds in first and third person. Verify hand/mouth alignment, one bubbling sound, lowering followed by a forward smoke puff, Stoned and the empty bong in the original hotbar slot. Release before five seconds and verify the bowl stays loaded with no exhale. Have a second player confirm they also see the completed exhale.
4. Fill all other inventory slots and repeat the load/smoke/reload cycle. The bong must never disappear or duplicate. Confirm the normal joint still works and that shared Stoned effects and bloodshot eyes behave as documented.

## Art provenance

The **built-in image generation tool** produced the glass material. The selected image was resized to 64x64 while preserving alpha; the model UVs select quiet regions for readable glass at inventory scale. A subsequent simplification attempt was discarded because it removed too much of the glass field. Geometry, preview rendering and audio are authored in the project scripts.

Generation prompt: “Use case: stylized-concept. Asset type: production material texture for a low-resolution voxel game's small clear-quartz glass vessel. Create one square seamless RGBA texture tile, front-on flat material, filling the entire canvas edge to edge. Cool nearly colorless pale blue-gray glass, subtle coarse pixel-art mottling, a few short white pixel glints, understated hand-made vintage voxel-game look. Actual transparency is essential: most of the tile is translucent pale blue, with only a few opaque white flecks. This is a flat glass material swatch for UV mapping onto a separately authored 3D model, NOT a drawing of a bong, bottle, window, cube, crystal, or object. No frame, borders, checkerboard pattern, gradients, labels, text, floor, perspective, cast shadow, or environment. Broad restrained color clusters that remain readable when resized to a 64 by 64 game texture. The background must really be transparent, never a painted checkerboard.”
