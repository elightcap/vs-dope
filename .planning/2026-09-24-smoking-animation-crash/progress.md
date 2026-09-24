# Progress

## 2026-09-24

- Read the user's screenshot, repository instructions, asset/dependency maps and current smoking implementation.
- Created `fix/smoking-animation-crash` in its own worktree from current master.
- Decompiled the installed 1.22.7 animation and shape classes; identified incomplete nullable offset vectors.
- Added a native animation-frame regression to MarijuanaProbe; assets remain unchanged for the first run.
- Baseline probe built and deployed cleanly, then reproduced `InvalidOperationException: Nullable object must have a value` at `Animation.lerpKeyFrameElement`, Animation.cs line 225, followed by the same GenerateFrame/GenerateAllFrames stack as the user's screenshot. It fails on `seraph-faceless/vsdope-smoke`.
- Completed lower-arm X/Z offsets in all four animation patches and added neutral frame-0 offsets for translating arms. Five-second use, frame count, sound and effects are unchanged.
- Rebuilt/deployed corrected assets and started the second live-server probe run.
- Fixed run: **61 checks passed**; both camera variants resolve their arm references and generate all 150 frames on both Seraph shapes. The original use-time, cancellation, consumption and Stoned-effect checks all pass. Builds report zero warnings/errors, and all 58 JSON patches apply without errors.
- Log review: no animation or mod exceptions. Existing coca recipe warning remains; resolving the full vanilla Seraph in the new probe exposes its duplicate `Eyes` attachment-point warning. Headless world generation also emitted a transient overloaded-tick warning.
- JSON semantic comparison confirms only zero offset components were added; existing rotations, held offsets, metadata and duration were preserved. `git diff --check` passes.
- Updated the animation contract, repository maps and manual acceptance steps. A graphical client is unavailable: restart with corrected assets, smoke for five seconds in first and third person, verify pose/audio/one consumed joint/Stoned, then test early cancellation.
