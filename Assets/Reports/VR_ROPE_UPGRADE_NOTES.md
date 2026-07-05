# VR Rope Upgrade Notes

## Goal
The previous rope behaved like a loose cloth/chain because each internal node was pulled down by gravity while both endpoints were already fixed by the pendulum. For a heavy swinging paint bucket, the rope should be visually tensioned most of the time, with only small controlled vibration and sag.

## Main Changes
- Rebuilt `RigRopeController` as a hybrid tensioned cable solver.
- Rope simulation now runs in `LateUpdate` so it follows the final bucket/anchor position of the current frame.
- Added a `TensionedCable` visual mode as the default.
- Increased quality from 18 segments / 20 iterations to 30 segments / 80 iterations.
- Reduced fake gravity on the rope so it no longer collapses downward.
- Added stiffness controls:
  - `tautness`
  - `shapeFollow`
  - `bendStiffness`
  - `maxSag`
  - `slackDetectionRange`
- Added small dynamic waves driven by bucket velocity, not random rope looseness.
- Added rope-type physics presets:
  - Cotton: more flexible, slightly more sag.
  - Nylon: strong/tight default.
  - Steel: near-rigid cable, almost no sag.
- UI stats now show rope mode, tension, slack percent, and segment count.

## Recommended Defaults
- Rope Type: Nylon
- Visual Mode: TensionedCable
- Segments: 30
- Constraint Iterations: 80
- Tautness: 0.97
- Shape Follow: 0.94
- Bend Stiffness: 0.965
- Max Sag: 0.05
- Dynamic Wave Amplitude: 0.014

## How to Tune Quickly
- Rope still too loose: increase `tautness` to 0.985 and `shapeFollow` to 0.97.
- Rope too perfectly straight: increase `dynamicWaveAmplitude` slightly to 0.02 or `maxSag` to 0.07.
- Rope too expensive: reduce `segmentCount` to 24 and `constraintIterations` to 50.
- Rope should look like steel cable: choose Steel Rope from the dropdown.
