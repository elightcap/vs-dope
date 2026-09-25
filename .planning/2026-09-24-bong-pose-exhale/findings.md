# Findings

- User screenshot shows the bong below the hand. Both variants share incorrect hand/GUI transforms in the generator.
- VS 1.22.7 inventory rendering automatically rotates blocks 180 degrees around X, but does not do so for items. The bong geometry is Y-up; GUI Y points downward.
- Held rendering applies the attachment matrix, translates by origin, scales, translates by attachment position plus item translation, rotates, then subtracts origin. A near-zero translation leaves the grip far from the hand.
- Preserve latest master Stoned/tolerance behavior and the five-second reusable-item workflow.
- Native animation vectors must contain all XYZ components. Validate generated frames, not only JSON.
- Particle properties and networking are available through the native server SpawnParticles API; emit only after successful consumption.

- The native hand attachment includes a -180-degree Y rotation. Both current camera render paths select TpHandTransform; FP settings are mirrored for compatible renderers.
- Seraph mouth geometry is on the negative-X head face at local y=.6...9 and z=2..3 model units. Dedicated bong poses bend the elbow below the hand and rotate ItemAnchor at the wrist. Weight 100 keeps the narrow rim at the mouth despite body-idle blending (measured gap: TP .0114 blocks, FP .0083).
- PlayerAnimationManager stops held idle/ready during use. Its FP held idle has zero weight outside the arm and requires a body idle in the test harness; testing it alone produced NaN matrices. Tests now include the real vanilla idle/idle-fp animation.
- Native server SimpleParticleProperties serialization preserves quad model, spawn position, velocity, opacity and size evolution. Smoke is emitted once after the loaded bowl is replaced; null dualCallByPlayer broadcasts to the smoker too.
