# Progress

2026-09-24: Created fix/bong-pose-and-exhale worktree from latest origin/master. Inspected the screenshot, current bong behavior, vanilla assets and decompiled 1.22.7 inventory/held renderers. Confirmed why the geometry-only preview missed the defects.

Implemented explicit inventory/ground transforms and a grip-centred hand transform in generator and itemtype. Added dedicated complete-vector arm/wrist animations for both Seraph shapes and camera variants, lowering before completion. Added server-only forward/upward expanding smoke after a successful five-second use.

Validation: .NET 10 mod/probe builds succeeded with zero warnings/errors; deployed via deploy.sh. Native 1.22.7 server: 157 checks passed, 64 patches applied without errors. Tests cover native GUI matrix orientation, ClientAnimator hand/mouth placement with body/held idles, complete keyframe generation, particle packet round-trip, cancellation/death/duplicate suppression and three full-inventory reuse cycles. Generator is deterministic; JSON and git diff checks pass. Existing coca ingredient and vanilla duplicate Eyes warnings remain unrelated.

During validation, corrected test-only double-to-float attachment rotations and added the missing body idle to the FP test harness. Refined wrist rotation to keep the elbow below the hand; inspected software skeleton geometry. No graphical client available: final client acceptance is documented in docs/BONG.md.

Verified work prepared for publication on fix/bong-pose-and-exhale; merge remains a separate user action.
