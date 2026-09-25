# Findings

- User reports the bowl/downstem faces toward the player while smoking; it must face outward.
- Latest master includes the previous pose/exhale fix and the cannabis rename; preserve both.
- Native attachment adds -180 degrees to item Y rotation and applies XYZ rotations. The old effective Rz(45) can become Rz(45)Ry(180) using item rotation (0, 0, -45). This reverses bowl direction without moving the neck axis, grip or mouthpiece.
- Prior tests checked grip and rim placement but omitted bowl direction. Extend the existing native presentation checks to compare bowl direction with the player's forward direction in idle and use poses.
