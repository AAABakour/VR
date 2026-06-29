# Architecture Map

This map describes the project structure after Phase 4. It separates current legacy prototype behavior from the rope, bucket, GPU SPH benchmark, and bucket SPH integration architecture.

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

Responsibility:

- GPU fluid data model.
- Solver configuration.
- Compute dispatch ownership.
- Particle buffers and simulation state.

Current status:

- Phase 3 implements `GpuSphSolver`, `GpuSphSettings`, `GpuSphDebugStats`, `GpuSphBufferUtility`, and `SphKernelNames`.
- `Assets/Shaders/SPH/GpuSph.compute` contains initialization, grid build, density/pressure, force, integration, and box collision kernels.
- Phase 3.1 adds runtime settings copies, grid overflow counters, and expanded debug stats.
- Phase 4 adds solver domain support for `FluidBox`, `BucketCylinder`, and `OpenWorldWithBounds`.
- Phase 4 adds GPU kernels for bucket volume initialization, nozzle initialization, nozzle emission, and cylindrical bucket collisions.
- Phase 4.2 corrects coordinate frames: `BucketCylinder` uses the bucket transform, while `OpenWorldWithBounds` uses the solver/root transform for external nozzle emission.
- `GpuSphSolver` exposes world-to-simulation conversion APIs so emitters can pass real world nozzle position, direction, and inherited velocity without assuming bucket-local space.
- The current solver is connected to the bucket in the Phase 4 scene but remains independent from the canvas.

## SPH Rendering

Location:

- `Assets/Scripts/SPH/Rendering`

Responsibility:

- GPU particle rendering.
- Surface extraction or screen-space fluid rendering.
- Debug draw modes and render stride handling.

Current status:

- Phase 3 implements `GpuSphParticleRenderer`.
- Particles render from GPU buffers with procedural indirect drawing and render stride.
- `Assets/Materials/SPH/MAT_GpuSphParticle.mat` is assigned in the benchmark scene.
- No particle GameObjects are created.

## SPH Collision

Location:

- `Assets/Scripts/SPH/Collision`

Responsibility:

- Collision boundary encoding.
- Bucket, nozzle, canvas, and world collision inputs.
- Impact event production within profile budgets.

Current status:

- Phase 3 implements `FluidBoxController` for transparent rectangular box bounds, motion-frame gravity, collision sizing, and runtime glass/edge visuals.
- `Assets/Materials/SPH/MAT_TransparentFluidBox.mat` is assigned in the benchmark scene.
- Phase 4 implements `BucketSphCollisionProvider`, translating `BucketCollisionProxy`, nozzle, bucket transform, and bucket motion into solver collision parameters.
- Canvas collision and paint impacts are still deferred.

## SPH Integration

Location:

- `Assets/Scripts/SPH/Integration`

Responsibility:

- Bridges between bucket motion, solver state, emission, impacts, and painting.
- High-level scene integration scripts.

Current status:

- Phase 3 implements `FluidBoxMotionController` and `FluidBoxBenchmarkController`.
- The benchmark applies existing simulation profiles and coordinates reset/profile/motion controls.
- Phase 4 implements `BucketSphFluidController`, `BucketSphNozzleEmitter`, `BucketSphMode`, and `BucketSphSubsystemAdapter`.
- The Phase 4 integration keeps legacy paint available but disables it by default only in the Phase 4 scene.
- Phase 4.2 makes `NozzleEmission` the default demo mode and treats `InternalAndEmission` as postponed until a two-domain or two-solver design is added.

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
- Phase 3 adds `UI/Debug/FluidBoxBenchmarkUI.cs` for the independent SPH benchmark.
- Phase 4 adds `UI/Debug/BucketSphDebugUI.cs` for bucket SPH mode/profile/emitter inspection.
- Phase 4.2 adds keyboard controls and a more complete SPH readout for mode, profile, particle counts, render stride, memory, grid overflow, nozzle world position, and solver buffer state.

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

Phase 3 benchmark scene:

- `Assets/Scenes/Benchmarks/FluidBoxBenchmarkScene.unity`

Phase 4 duplicated integration scene:

- `Assets/Scenes/Integrated/MainSimulationScene_Phase04_BucketSphNozzle.unity`

Future benchmark location:

- `Assets/Scenes/Benchmarks`

The benchmark folder now contains the independent transparent fluid box GPU SPH scene.

## Phase Reports

- `Assets/Docs/Phase01_ArchitectureCleanup_Report.md`
- `Assets/Docs/Phase02_RopeBucketRig_Report.md`
- `Assets/Docs/Phase03_FluidBoxGpuSph_Report.md`
- `Assets/Docs/Phase03_1_Finalization_Report.md`
- `Assets/Docs/Phase04_BucketSphNozzle_Report.md`

## Future Integration Path

Phase 5 should bridge bucket SPH particles into canvas painting only after Phase 4 scene validation:

- Extract sparse impact events without full particle CPU readback.
- Feed canvas painting through a controlled impact bridge.
- Keep the legacy paint emitter available until parity is proven.
