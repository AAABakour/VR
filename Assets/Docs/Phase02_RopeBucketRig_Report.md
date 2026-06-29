# Phase 2 Rope/Chain and Bucket Rig Report

## Scope

Phase 2 introduces a duplicated integration scene and non-invasive rig architecture for the rope/chain visual and bucket metadata layer. The legacy pendulum, paint emission, particle simulator, canvas painter, UI, and reset flow remain in place.

Scene:

- `Assets/Scenes/Integrated/MainSimulationScene_Phase02_RopeBucketRig.unity`

## Scene Wiring

The Phase 2 scene was duplicated from the Phase 1 integration scene and keeps the Phase 1 architecture root:

- `ArchitectureRoot`
- `SimulationLifecycleManager`
- `SceneReferenceValidator`
- `LegacyPendulumSubsystem`
- `LegacyPaintSubsystem`

New Phase 2 scene objects:

- `RopeRig`
- `Bucket/BucketRig`
- `Bucket/HandlePivot` with `BucketHandleRig`

Legacy visual conflict handling:

- The old single-cylinder `Rope` GameObject is inactive in the Phase 2 scene.
- `PendulumController.updateLegacyRopeVisual` is disabled in the Phase 2 scene only.
- The old `BucketHandleMotion` component is disabled in the Phase 2 scene so `BucketHandleRig` owns handle motion.

The original `Assets/MainSimulationScene.unity` remains unchanged, and the old reset behavior remains owned by the existing `SimulationManager`.

## Rope Rig

Location:

- `Assets/Scripts/RopeRig`

Implemented components:

- `RopeRigMode`
- `RopeRigSegment`
- `RopeRigController`

Current behavior:

- Connects `PivotPoint` to `Bucket/HandlePivot/RopeAttachPoint`.
- Supports rope or chain visual modes.
- Generates primitive fallback segments when no prefab is assigned.
- Updates segment placement in `LateUpdate`.
- Provides gizmo diagnostics for endpoints and the rope path.

This is a visual and attachment architecture layer only. It does not replace the pendulum solver with a physical rope constraint yet.

## Bucket Rig

Location:

- `Assets/Scripts/Bucket`

Implemented components:

- `BucketRigController`
- `BucketMotionDataProvider`
- `BucketCollisionProxy`
- `BucketHandleRig`

Current behavior:

- Centralizes bucket references for the body, handle pivot, rope attach point, nozzle point, motion provider, and collision proxy.
- Samples bucket velocity from the legacy `PendulumController`.
- Provides an approximate collision/nozzle proxy for future fluid boundaries.
- Drives stable handle motion from bucket velocity without replacing the bucket transform motion.

This is a metadata and integration layer for future SPH work. It does not implement fluid collision, internal fluid, or canvas rewrite behavior.

## Profiles

The initial profiles in `Assets/Settings/SimulationProfiles` were updated with realistic future-facing targets:

- `Profile_LegacyPrototype`: legacy paint stack active, Phase 2 rig enabled.
- `Profile_Debug`: 50k particle future-SPH debug target.
- `Profile_Presentation`: 250k particle presentation target.
- `Profile_ProfessorBenchmark`: 1M particle benchmark target.
- `Profile_VRSafe`: 100k particle conservative VR target.

The active scene profile remains legacy-safe, so current paint behavior still works before GPU SPH exists.

## Deferred

Not implemented in Phase 2:

- GPU SPH solver.
- Internal bucket fluid simulation.
- Fluid-canvas painting rewrite.
- Production rope physics constraints.
- Replacement of `SimulationManager`.

## Phase 3.1 Finalization Notes

Phase 3.1 hardened the Phase 2 rig foundation:

- Added lifecycle adapters:
  - `RopeRigSubsystemAdapter`
  - `BucketRigSubsystemAdapter`
- Confirmed execution order:
  - `BucketMotionDataProvider`: earlier
  - `BucketHandleRig`: after motion sampling
  - `RopeRigController`: after handle motion
- `BucketCollisionProxy` now supports an explicit `nozzlePoint` transform while preserving the fixed local fallback.
- The Phase 2 scene wires the rope and bucket rig adapters into `SimulationLifecycleManager`.
- The legacy rope cylinder remains inactive in the Phase 2 scene, and the original main scene remains backward-compatible.
