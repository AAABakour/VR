# Phase 4 Bucket SPH Nozzle Report

## Goal

Phase 4 connects the GPU SPH foundation to the swinging bucket rig without replacing the legacy canvas paint path. The new integration scene validates bucket-local fluid particles, nozzle emission, and explicit architecture wiring while preserving the Phase 2 rope/bucket work and the Phase 3 FluidBox benchmark.

## Scene

- Created `Assets/Scenes/Integrated/MainSimulationScene_Phase04_BucketSphNozzle.unity` from the Phase 2 integration scene.
- Added `SPH_BucketFluidRoot` with:
  - `GpuSphSolver`
  - `GpuSphParticleRenderer`
  - `GpuSphDebugStats`
  - `BucketSphCollisionProvider`
  - `BucketSphFluidController`
  - `BucketSphSubsystemAdapter`
- Added child objects:
  - `BucketSphNozzleEmitter`
  - `BucketSphDebugUI`
- Added the bucket SPH subsystem adapter to `SimulationLifecycleManager.explicitSubsystemBehaviours`.
- Enabled Phase 4 validation on `SceneReferenceValidator`.
- Disabled legacy `PaintEmitter` and `PaintParticleSimulator` only in the Phase 4 scene. The old systems and references remain present so reset and legacy behavior can be restored without deleting assets.

## Runtime Scripts

- `BucketSphFluidController` coordinates mode selection, solver domain, profile application, reset, pause, and legacy paint disabling.
- `BucketSphNozzleEmitter` emits particle batches directly on the GPU through a solver API. It does not create particle GameObjects and does not read back full particle buffers.
- `BucketSphCollisionProvider` converts `BucketCollisionProxy`, nozzle, bucket transform, and bucket motion into solver-ready local collision parameters.
- `BucketSphSubsystemAdapter` exposes the bucket SPH controller to the lifecycle manager.
- `BucketSphDebugUI` shows mode, profile, particle counts, GPU memory estimate, grid overflow, and emitter state.

## Solver And Kernels

`GpuSphSolver` now supports three domains:

- `FluidBox`
- `BucketCylinder`
- `OpenWorldWithBounds`

New compute kernels:

- `InitializeBucketVolume`
- `InitializeNozzleEmission`
- `EmitFromNozzle`
- `HandleBucketCollisions`

Existing FluidBox kernels are preserved:

- `InitializeParticles`
- `ClearGrid`
- `BuildGrid`
- `ComputeDensityPressure`
- `ComputeForces`
- `Integrate`
- `HandleBoxCollisions`

## Testing Modes

Use `BucketSphFluidController.startupMode` or the debug UI helper methods:

- `InternalBucketFluid`: fills the bucket volume and uses cylindrical bucket collision.
- `NozzleEmission`: initializes and emits from the nozzle into an open bounded local volume.
- `InternalAndEmission`: keeps bucket collision active while allowing nozzle emission.
- `DebugStaticEmission`: emits from the nozzle without inherited bucket velocity for easier inspection.

Suggested smoke test:

1. Open `MainSimulationScene_Phase04_BucketSphNozzle.unity`.
2. Press Play.
3. Confirm the bucket still swings from the legacy pendulum/Phase 2 rig.
4. Confirm GPU particles render near or inside the bucket.
5. Cycle modes or change `startupMode`, then call reset.
6. Confirm the canvas remains visible but is not painted by SPH particles.

## Known Limitations

- SPH particles are not connected to `CanvasPainter` yet.
- There is no SPH impact bridge, sparse impact readback, splat batching, or GPU-to-canvas paint transfer.
- Bucket collision is a pragmatic cylindrical approximation from `BucketCollisionProxy`.
- Nozzle emission is GPU-side particle rewrites into a wrapped range, not a physical inflow solver.
- The renderer still uses particle billboards rather than surface extraction.
- Legacy CPU paint is disabled only in the Phase 4 scene and remains available for parity checks.

## Phase 4.1 Stabilization Fixes

- Verified `Assets/Shaders/SPH/GpuSph.compute` contains a single `ComputeDensityPressure` kernel function declaration and all solver-required kernels remain present.
- Confirmed the Phase 4 kernel set:
  - `InitializeParticles`
  - `InitializeBucketVolume`
  - `InitializeNozzleEmission`
  - `ClearGrid`
  - `BuildGrid`
  - `ComputeDensityPressure`
  - `ComputeForces`
  - `Integrate`
  - `EmitFromNozzle`
  - `HandleBoxCollisions`
  - `HandleBucketCollisions`
- Added safe `BucketSphDebugUI` callbacks for direct mode switching, emission toggling, and profile application.
- Added a `BucketSphDebugUI` warning when `gridOverflowCount > 0`.
- Improved `BucketSphFluidController` so mode/profile changes resolve references, configure solver domain/emitter state, and reinitialize the solver consistently.
- Restored non-static emission modes to the configured default inherited bucket velocity after using `DebugStaticEmission`.
- Reconfirmed the Phase 4 scene keeps `PaintEmitter` and `PaintParticleSimulator` disabled by default only in `MainSimulationScene_Phase04_BucketSphNozzle.unity`.
- No canvas impact extraction or painting bridge is implemented in Phase 4.1.

## Phase 5 Focus

Phase 5 should add sparse GPU impact extraction and a controlled bridge from SPH impacts to canvas painting. That work should avoid full particle readback, avoid CPU particle loops, and keep legacy painting available until visual parity is proven.
