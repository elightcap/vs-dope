# Progress

2026-09-25: Created fix/bong-bowl-outward worktree from current origin/master (7ceb470). Confirmed PR #59 is merged. Calculated a neck-axis half-turn that preserves the previous grip and mouthpiece alignment while reversing the protruding bowl/downstem.

Updated generator and both held transforms with a local neck-axis half-turn. The only generated asset changes are the held rotations. Extended native presentation checks with 16 idle/use bowl-direction assertions across empty/loaded, both Seraph models and both camera variants. Inspected a software skeleton/geometry study with the corrected orientation.

Build and probe: zero warnings/errors. Deployed through deploy.sh into isolated test data. Native 1.22.7 server: 179 checks passed; all 64 JSON patches applied without errors. Existing vanilla duplicate Eyes warning only. Generator output deterministic; JSON and git diff checks pass.

An initial copied test-server configuration retained absolute paths to the old mod/save folders and ran the old probe. Corrected those paths to the isolated data directory; the recorded 179-check run loads cannabisprobe and the current task build. Server shut down cleanly afterward.

Graphical client acceptance: after updating, hold/smoke either bong in first and third person; bowl and downstem must point away from the character while the neck stays in the hand and mouthpiece reaches the mouth. No graphical client is available here.
