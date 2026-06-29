# Architecture Map

This map describes the project structure after Phase 2. It separates current legacy prototype behavior from the new rope, bucket, and future fluid simulation architecture.

## Core

Location:

- `Assets/Scripts/Core`

Purpose:

- Shared simulation orchestration.
- Reset/pause/profile lifecycle contracts.
- Simulation-wide validation and mode/profile definitions.

Current files:

- `Core/Architecture/ISimulationSubsystem.cs`
- `Core/Architecture/SimulationLifecycleManager.cs`
- `Core/Architecture/LegacySystemTag.cs`
- `Core/Profiles/SimulationMode.cs`
- `Core/Profiles/SimulationProfile.cs`
- `Core/Validation/SceneReferenceValidator.cs`

## RopeRig

Location:

- `Assets/Scripts/RopeRig`

Responsibility:

- Chain or rope segment rig.
- Constraint setup.
- Stable bucket attachment.
- VR-safe rig behavior.

Current status:

- Phase 2 implements `RopeRigMode`, `RopeRigSegment`, and `RopeRigController`.
- The Phase 2 integration scene uses `RopeRig` to connect `PivotPoint` to `Bucket/HandlePivot/RopeAttachPoint`.
- The old single-cylinder rope is disabled only in the Phase 2 scene.
- The rig is currently visual/architectural; production rope constraints are deferred.

## Bucket

Location:

- `Assets/Scripts/Bucket`

Responsibility:

- Bucket body rig.
- Nozzle/handle attachment metadata.
- Bridge data for internal fluid and external emission.

Current status:

- Phase 2 implements `BucketRigController`, `BucketMotionDataProvider`, `BucketCollisionProxy`, and `BucketHandleRig`.
- Current bucket motion still comes from `PendulumController`.
- The bucket rig centralizes body, handle, rope attach, nozzle, motion, and collision proxy references for later SPH phases.
- The legacy `BucketHandleMotion` component is disabled only in the Phase 2 scene so `BucketHandleRig` owns handle motion there.

## SPH

Location:

- `Assets/Scripts/SPH`

Future responsibility:

- GPU fluid data model.
- Solver configuration.
- Compute dispatch ownership.
- Particle buffers and simulation state.

Current status:

- Folder prepared only. No SPH solver was implemented in Phase 1.

## SPH Rendering

Location:

- `Assets/Scripts/SPH/Rendering`

Future responsibility:

- GPU particle rendering.
- Surface extraction or screen-space fluid rendering.
- Debug draw modes and render stride handling.

Current status:

- Folder prepared only.

## SPH Collision

Location:

- `Assets/Scripts/SPH/Collision`

Future responsibility:

- Collision boundary encoding.
- Bucket, nozzle, canvas, and world collision inputs.
- Impact event production within profile budgets.

Current status:

- Folder prepared only.

## SPH Integration

Location:

- `Assets/Scripts/SPH/Integration`

Future responsibility:

- Bridges between bucket motion, solver state, emission, impacts, and painting.
- High-level scene integration scripts.

Current status:

- Folder prepared only.

## Painting

Location:

- `Assets/Scripts/Painting`

Future responsibility:

- Production painting interfaces.
- GPU/CPU canvas bridge.
- Impact batching and presentation-ready paint output.

Current status:

- Folder prepared only. Current painting remains in `Assets/Scripts/Paint/CanvasPainter.cs`.

## UI

Location:

- `Assets/Scripts/UI`
- `Assets/Scripts/UI/Debug`

Future responsibility:

- Presentation UI.
- Debug overlays.
- Profile selection.
- Benchmark metrics.

Current status:

- Existing `SimulationStatsUI` remains in place.
- `UI/Debug` is prepared for future debug-only UI.

## Legacy

Location:

- `Assets/Scripts/Legacy`

Purpose:

- Non-invasive adapters and tags for current prototype systems.
- A clear boundary so future phases can replace legacy behavior deliberately.

Current Phase 1 files:

- `LegacyPaintSystemAdapter.cs`
- `LegacyPendulumAdapter.cs`

Legacy systems preserved in their original folders:

- `Assets/Scripts/Physics/PendulumController.cs`
- `Assets/Scripts/Paint/PaintEmitter.cs`
- `Assets/Scripts/Paint/PaintParticleSimulator.cs`
- `Assets/Scripts/Paint/CanvasPainter.cs`
- `Assets/Scripts/Core/SimulationManager.cs`
- `Assets/Scripts/UI/SimulationStatsUI.cs`

## Scenes

Current working scene:

- `Assets/MainSimulationScene.unity`

Phase 1 duplicated integration scene:

- `Assets/Scenes/Integrated/MainSimulationScene_Phase01_Architecture.unity`

Phase 2 duplicated integration scene:

- `Assets/Scenes/Integrated/MainSimulationScene_Phase02_RopeBucketRig.unity`

Future benchmark location:

- `Assets/Scenes/Benchmarks`

The benchmark folder is prepared for Phase 3. No benchmark scene was created in Phase 2.

## Phase Reports

- `Assets/Docs/Phase01_Architecture_Report.md`
- `Assets/Docs/Phase02_RopeBucketRig_Report.md`
