# Progress

## 2026-09-24

- Created a fresh worktree from current master, read repository instructions/maps and reviewed JointItem, SyringeItem, recipes, model/audio generators and live-runtime probes.
- Verified the grid dimensions against the installed game implementation and chose in-slot replacement to guarantee the empty vessel returns even with full inventory.
- Started implementation and asset creation.
- Added `BongItem`, empty/loaded variants, exact quartz and shapeless loading recipes, localized names/help, native 3D meshes, generated RGBA glass material and original 3.7-second synthesized bubbling audio.
- Kept JointItem and fixed smoking keyframes unchanged. Bong use shares five-second timing and Stoned behavior, with guarded server-side in-slot replacement for completed use.
- Initial build caught a missing `now` argument to `StonedSystem.Apply`; supplied the same calendar clock as JointItem. Probe compilation caught `ResolvedItemStack` casing; checked the DLL and corrected it. Added explicit nullable guards until both projects built without warnings.
- Initial live probe passed gameplay checks but caught a texture lookup bug in the test (AssetLocation string concatenation included its domain in the path). Corrected resolution to use the texture path in the item's domain and reran.
- Final live-server result: **101 checks passed**, including exhaustive recipe placements, early/cancel/death/switched-item/duplicate-stop safety, 3 complete reuse cycles with all other native inventory slots full, Stoned timing/healing, asset resolution and native animation generation. Main/probe builds: 0 warnings, 0 errors. JSON patch loader: 58 successful patches, no errors.
- Reviewed software previews and switched to quiet glass UV regions to remove stretched texture noise. Kept the decorative water volume inside the chamber. Static JSON/geometry/texture/UV validation and `git diff --check` pass; ffprobe confirms 3.7-second mono 22,050 Hz Vorbis.
- Log review found only existing coca recipe / vanilla Seraph attachment warnings and a transient world-generation overloaded tick. No new mod exceptions. Added docs/BONG.md and updated runtime/asset/dependency maps and marijuana documentation.
- Graphical-client acceptance remains: first-/third-person mouth alignment, glass rendering and bubbling sound; restart with new DLL/assets, craft/load/smoke/reload and test early release/full inventory as documented.
