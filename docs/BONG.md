# Reusable bong

Vintage Story 1.22.7 item addition. The empty item is named **Bong** and the filled item **Loaded up Bong**. Both have a stack limit of one.

## Recipes and use

Craft one Bong with two `game:clearquartz`, in these exact 3x3 positions:

| Left | Middle | Right |
| --- | --- | --- |
| Empty | Empty | Empty |
| Empty | Clear quartz | Empty |
| Empty | Clear quartz | Empty |

Combine **one Bong + one Marijuana buds** anywhere in a crafting grid to produce **one Loaded up Bong**. Loading consumes those two input items; it does not leave an extra empty bong behind. No water ingredient or refill is required.

Hold right-click for **five seconds** to smoke. The existing tested first-/third-person smoking animations raise the hand, and a distinct bubbling/inhalation sound starts after 0.9 seconds. Completing use replaces the loaded item with **one empty Bong in the same inventory slot**, including with a full inventory. The returned bong can immediately be loaded again. Releasing early, cancelling, dying or changing the held item grants no effect and does not consume the load. Repeated stop callbacks cannot return extra vessels.

The effect is the same **Stoned** effect as a joint: **two in-game hours**, **-20% base movement speed**, **0.5 HP per in-game minute**, **+25% hunger**, and **-25% creature detection factor**. Joints and bongs share marijuana-specific tolerance, which scales all these magnitudes. Subsequent uses refresh the expiry without stacking the rate or speed penalty. Bloodshot eyes and the Stoned HUD use the existing shared systems. Balance and animation fixes for joints also apply to this route. See `DRUG_TOOLS.md` for detection limits and tolerance checks.

## Implementation and assets

- `Items/BongItem.cs` is registered as `vs-dope.bong`, with codes `bong-empty` and `bong-loaded`. It uses temporary per-stack use/audio flags and server-side inventory/effect changes. Non-stackable variants permit direct slot replacement, without an inventory insertion or drop fallback.
- `recipes/grid/bong-empty.json` uses a full 3x3 shaped pattern `___,_Q_,_Q_`. `bong-loaded.json` is shapeless. `itemtypes/bong.json` supplies variant shapes, texture aliases and GUI/hand/ground transforms.
- `shapes/item/bong-{empty,loaded}.json` are portable native Model Creator shapes: a stepped glass chamber, thick base, narrow hollow neck, open mouthpiece and attached downstem/bowl. The loaded variant adds a green bud cluster using the existing marijuana atlas.
- `textures/item/bong-glass.png` is a 64x64 RGBA generated glass texture. Logical UV dimensions are 64x64. Quiet glass UV `[43,41,45,43]` and rim UV `[35,52,37,54]` avoid stretching the full tile over narrow faces. Glass elements use render pass 3, following vanilla `shapes/item/clutter/art/bottle.json`.
- `tools/build_bong_models.py` regenerates item/models deterministically. `tools/preview_bong_models.py` renders their actual geometry and textures with approximate alpha blending to `docs/previews/bong-items.png`; it is a software preview, not a game screenshot.
- `sounds/player/bong-bubbles.ogg` is original synthesized resonant bubbling under soft inhalation noise. `tools/build_bong_audio.py` generates 3.7 seconds of mono 22,050 Hz Vorbis from a fixed seed (numpy/scipy/ffmpeg), without recordings or external samples. Server broadcast is guarded to play once per held use.

## Verification

`tests/MarijuanaProbe/BongChecks.cs` extends the existing test-only live-server mod. It tests real recipe matching and input consumption, all 36 pairs of quartz positions, all 72 distinct loading positions, rejected extra/missing ingredients, empty/early/cancel/death/switched-item/duplicate-stop safety, and three complete load/smoke/reload cycles in a full native inventory. It checks that exactly one bud is consumed each cycle, one empty bong returns in-place, Stoned refreshes without stacking, and one game minute heals 0.5 HP. Models/textures and the bubbling sound must resolve. The existing native `SmokingAnimationChecks` also runs against both patched Seraph shapes and both camera variants.

Build/deploy instructions are in `docs/MARIJUANA.md`. Never distribute the test probe with the mod. The server's existing coca recipe warning and duplicate `Eyes` attachment-point warning in the full vanilla Seraph are unrelated.

Result on 1.22.7: **101 checks passed** (61 existing marijuana/animation checks plus 40 bong checks). Main mod and probe builds have zero warnings/errors; all 58 existing JSON patches apply without errors. All new model faces have valid texture keys/UVs and finite geometry; the sound is verified as 3.7 seconds of mono Vorbis at 22,050 Hz.

Client acceptance (requires a rendered game):

1. Restart with the updated DLL/assets. In creative, inspect Bong and Loaded up Bong in inventory, in hand and dropped on the ground. Check the translucent glass and visible loaded bowl.
2. Craft with quartz in centre and bottom-centre. Move either quartz elsewhere and verify the output disappears. Load the bong with one bud in different grid positions.
3. Smoke for five seconds in first and third person. Verify hand/mouth alignment, one bubbling sound, Stoned and the empty bong in the original hotbar slot. Release before five seconds and verify the bowl stays loaded.
4. Fill all other inventory slots and repeat the load/smoke/reload cycle. The bong must never disappear or duplicate. Confirm the normal joint still works and that shared Stoned effects and bloodshot eyes behave as documented.

## Art provenance

The **built-in image generation tool** produced the glass material. The selected image was resized to 64x64 while preserving alpha; the model UVs select quiet regions for readable glass at inventory scale. A subsequent simplification attempt was discarded because it removed too much of the glass field. Geometry, preview rendering and audio are authored in the project scripts.

Generation prompt: “Use case: stylized-concept. Asset type: production material texture for a low-resolution voxel game's small clear-quartz glass vessel. Create one square seamless RGBA texture tile, front-on flat material, filling the entire canvas edge to edge. Cool nearly colorless pale blue-gray glass, subtle coarse pixel-art mottling, a few short white pixel glints, understated hand-made vintage voxel-game look. Actual transparency is essential: most of the tile is translucent pale blue, with only a few opaque white flecks. This is a flat glass material swatch for UV mapping onto a separately authored 3D model, NOT a drawing of a bong, bottle, window, cube, crystal, or object. No frame, borders, checkerboard pattern, gradients, labels, text, floor, perspective, cast shadow, or environment. Broad restrained color clusters that remain readable when resized to a 64 by 64 game texture. The background must really be transparent, never a painted checkerboard.”
