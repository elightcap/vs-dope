# Reusable syringes

## Player use

- Craft one empty syringe using the centre column: metal rod, clear quartz, metal plate. Rods accept copper, tin, brass, gold, iron or steel. The plate may be any metal and need not match the rod. Vintage Story 1.22.7 uses `game:rod-*`; a small patch adds tin and brass rods to the vanilla rod item and smithing recipe, with matching density, recycling and English names.
- Craft an empty syringe together with one portable liquid vessel holding at least 1 L of heroin or morphine solution. The vessel remains and loses 1 L.
- For partial refills or top-ups, right-click a placed liquid vessel, or sneak-right-click with one vessel in the off hand. Sealed barrels must be opened first. A partially filled syringe only accepts the same liquid.
- Hold right-click for 1.5 seconds to apply one 0.1 L dose. A full syringe holds 10 doses. The tenth use returns the empty, reusable syringe. Cancelled interactions consume nothing.
- The tooltip reports litres and complete doses remaining. A fractional remainder below 0.1 L is retained but cannot be applied until topped up.

Morphine filling uses the existing **morphine solution** liquid. The solid morphine item remains unchanged. Heroin applies the existing tolerance-scaled 50% slowdown for one in-game hour; repeat applications restart that duration. Morphine retains its existing heal, slow, intoxication, duration, tolerance and overdose behavior.

## Inventory artwork

Four new transparent 64x64 PNGs live in `assets/vs-dope/textures/item/`: `syringe-empty.png`, `syringe-heroin.png`, `syringe-morphine.png`, and `opium-paste-icon.png`. The syringe is aged metal with a quartz barrel; heroin has amber fill and morphine pale blue-green fill. The opium icon updates the existing opium item; it does not add another processing stage.

Generated using the built-in image tool, then nearest-neighbour normalized to the game's power-of-two texture size. Prompt set:

1. Empty antique reusable syringe, steel needle bottom-left, quartz barrel and aged brass/steel plunger top-right. Vintage Story-style muted earthy pixel art, crisp chunky pixel clusters, limited palette, transparent background, no text or frame.
2. Edit the empty syringe: preserve silhouette, metal, orientation, pixel clusters and transparency; fill the chamber with warm amber liquid for heroin.
3. Edit the empty syringe with the same invariants; fill the chamber with pale blue-green liquid for morphine.
4. Small irregular brownish-ivory opium paste mound, rough folded facets and cream flecks, rustic pixel art, transparent background, no container or utensils.

## Validation status

JSON parsing, recipe/variant/texture/localization cross-checks and PNG dimensions/alpha were checked in the development workspace. The implementation was reviewed against the public Vintage Story liquid-container and recipe APIs.

Compiled successfully against the official Vintage Story 1.22.7 server assemblies using .NET SDK 10.0.401 (zero errors; six existing nullable warnings). The isolated server reached RunGame and all new JSON patches applied without errors. All 53 runtime integration checks passed for the registered recipes, both liquid types, volume transitions, refill conservation and serialization. Build with `VINTAGE_STORY` pointing at the installation root and `dotnet build`. References are `VintagestoryAPI.dll`, `VintagestoryLib.dll`, and `Mods/VSSurvivalMod.dll`.

Client interaction, animation, movement-effect timing and multiplayer playtesting remain pending. Server startup reports the existing unrelated coca recipe reference to `game:aquavitaeportion`; this change does not alter that processing recipe.

## In-game acceptance checks

1. Craft using each allowed rod, including a rod/plate metal mismatch. Confirm the exact centre-column recipe, one output and one of each input consumed; ordinary quartz must not match.
2. Use heroin and morphine solution separately in buckets, bowls, jugs and supported modded vessels. Crafting removes exactly 1 L and retains the vessel; empty/wrong-liquid/underfilled/stacked vessels must not craft.
3. From a full syringe, complete ten applications: 0.9, 0.8 ... 0.1 L, then empty. An eleventh application does nothing. Cancel early and confirm no dose/effect.
4. Top up partially filled syringes from placed/off-hand vessels; confirm equal volume loss in the source and gain in the syringe, never exceeding 1 L. Mixing liquids, drawing from sealed barrels and using another player's protected vessel must fail.
5. Move/drop/pick up the partly used syringe, then save/reload and reconnect in multiplayer. Confirm content and volume persist, and no dose is applied twice.
6. At zero tolerance, check heroin halves movement speed for one calendar hour; dose again to restart the timer. Check heavier use updates tolerance and overdose exactly once per application, including rapid repeat uses. Verify morphine matches its existing effect.
7. Inspect all four sprites in the inventory, hotbar and hand. Confirm readable silhouettes, transparent backgrounds, and distinct empty/heroin/morphine states.

## Reproducing the server integration checks

`tests/SyringeProbe` is a test-only mod that runs against loaded game items and recipes at server RunGame. Build it with `dotnet build tests/SyringeProbe/SyringeProbe.csproj` and the same `VINTAGE_STORY` environment variable. In a disposable, localhost-only test server, install the built vs-dope mod normally, then install the probe DLL and its `modinfo.json` in a separate Mods/syringeprobe directory. Look for `SYRINGE TEST SUMMARY` in the server log, or `SYRINGE TEST FAILED` for a failed assertion. Do not include the probe in a normal mod distribution.

Checks use the real registered items, native container implementation and native recipe consumption, not substitutes. They verify all six rods, recipe placement and mixed metals; both liquids' fill cap, conservation, ten dose-volume transitions, empty conversion and eleventh-dose rejection; partial refill, serialization roundtrip, mixing rejection, top-up conservation, and crafting vessel retention/volume consumption/stack guard. Player effects and input handling still require the client playtest above.
