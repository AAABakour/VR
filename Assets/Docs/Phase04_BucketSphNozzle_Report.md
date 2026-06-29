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

## Phase 4.2 Demo Readiness and World-Space Emission Fixes

Phase 4.2 was needed because the Phase 4 scene had the architecture in place but did not demonstrate the feature clearly in Play Mode. The old legacy stats UI could still suggest `Particles: 0`, the SPH particles were too subtle, and nozzle emission risked being interpreted in the bucket frame after particles left the nozzle.

The main technical correction is coordinate-frame separation:

- `InternalBucketFluid` uses `GpuSphSimulationDomain.BucketCylinder`.
- Bucket-cylinder particles are initialized and rendered in the bucket frame.
- Bucket gravity can include bucket-local/inertial effects from `BucketSphCollisionProvider`.
- `NozzleEmission` uses `GpuSphSimulationDomain.OpenWorldWithBounds`.
- Open-world particles are emitted from the real `PaintNozzle` / `nozzlePoint` world position.
- `BucketSphNozzleEmitter` now asks `GpuSphSolver` to convert world nozzle position, direction, and inherited bucket velocity into the active simulation frame.
- In open-world mode, `GpuSphSolver` uses the solver/root transform as the simulation frame, so already-emitted particles fall independently in world/root space instead of staying visually locked to the swinging bucket.
- `GpuSphParticleRenderer` renders using the solver's active simulation local-to-world matrix.

Default Phase 4 scene behavior:

- `MainSimulationScene_Phase04_BucketSphNozzle.unity` starts in `NozzleEmission`.
- Debug profile is used by default.
- Emission is enabled immediately.
- Legacy `PaintEmitter` and `PaintParticleSimulator` remain in the scene but are disabled.
- Legacy `SimulationStatsUI` is hidden in this Phase 4 scene so the demo does not show misleading CPU paint particle counts.

Keyboard controls shown by `BucketSphDebugUI`:

- `R`: reset Bucket SPH.
- `1`: internal bucket fluid mode.
- `2`: nozzle emission mode.
- `3`: internal plus emission is marked as future work.
- `E`: toggle emission.
- `F1`: Debug profile.
- `F2`: Presentation profile.
- `F3`: Professor 1M profile.
- `F4`: VR Safe profile.

Mode testing:

- Internal mode: press `1`, then `R`. Particles initialize inside the bucket cylinder and render in the bucket frame.
- Nozzle mode: press `2`, then `R` or wait for emission. Particles emit from the moving nozzle, inherit bucket velocity, and fall in world/root space.
- Professor 1M: press `F3`. The profile remains supported through its render stride.

`InternalAndEmission` is postponed in Phase 4.2. A single solver has one coordinate frame at a time, so correct simultaneous bucket-local internal fluid and world-space external emission should use a two-solver or mixed-domain architecture in a later phase. Phase 4.2 does not fake this mode.

Known Phase 4.2 limitations:

- SPH still does not paint the canvas.
- There is no SPH impact extraction.
- There is no GPU-to-canvas bridge.
- No full particle readback or CPU particle simulation is added.
- Bucket collision remains an approximate cylinder from `BucketCollisionProxy`.

Canvas painting remains Phase 5 work.

## Phase 5 Focus

Phase 5 should add sparse GPU impact extraction and a controlled bridge from SPH impacts to canvas painting. That work should avoid full particle readback, avoid CPU particle loops, and keep legacy painting available until visual parity is proven.
