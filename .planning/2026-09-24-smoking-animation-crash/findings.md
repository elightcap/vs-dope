# Findings

- User crash: `InvalidOperationException: Nullable object must have a value` in `Animation.lerpKeyFrameElement`, reached from `ClientAnimator.AnimNowActive` while smoking on 1.22.7.
- Verified against the installed 1.22.7 API DLL: `PositionSet` is true when any offset axis is present, but interpolation dereferences all three nullable axes. Rotation and stretch have the same contract.
- Both `vsdope-smoke` and `vsdope-smoke-fp` specify only `offsetY: 1` for LowerArmR at frames 27, 130 and 149, in both Seraph patches. Missing X/Z offsets explain the reported exception.
- Vanilla Seraph animation keyframes provide complete XYZ vectors. Neutral frame 0 should explicitly provide zero offsets for arms that translate later, so the movement starts from neutral instead of wrapping a held translation.
- Verified `Shape.InitForAnimations(ILogger, string, params string[])` resolves frame references and joints, and `Animation.GenerateAllFrames(ShapeElement[], Dictionary<int, AnimationJoint>, bool)` exercises the failing code without a graphical client.
- The previous 54-check probe parsed animation assets but used a stand-in animation manager for item interactions; it never generated animation frames. Extend it to generate both camera animations from both actual patched shapes.
- Runtime used for verification: Vintage Story 1.22.7; .NET 10. Fresh worktree branches from merged master `474916c`.
