# VR Integrated Merge Notes

This project was created by merging the rope branch into the collision/painting branch.

## Base project
- `SwingingPaintBucketSimulation-final` was used as the base because it contains the latest canvas collision/painting work.

## Merged from rope branch
- `RigRopeController`
- `RopeTypeUIController`
- rope segment prefab
- cotton / nylon / steel rope materials
- rope textures
- Rope Type dropdown UI in `MainSimulationScene`

## Preserved from collision branch
- `PaintSurfaceStateV2`
- `PaintDripSolverV2`
- `PaintImpactEngineV2` with surface-state accumulation
- advanced canvas impact/drip behavior
- bucket handle visual motion setup

## Integration decisions
- The collision branch scene remains the master scene.
- The old simple rope mesh is disabled in the scene and kept only as a safe legacy reference.
- `RigRopeSystem` is added as a clean root object. It follows `PivotPoint` and `RopeAttachPoint`.
- `PendulumController` no longer requires the simple rope transform to update bucket motion.
- `SimulationStatsUI` now shows rope tension/type, impact state, coverage, wetness, and drip transfer stats.

## Next improvement target
The next high-impact section is the collision/painting system: UV-space accumulation, paint thickness maps, wet/dry behavior, realistic dripping/smearing, and higher-quality splat generation.
