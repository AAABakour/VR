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

## Phase 4.3 Active Particle Lifecycle and Demo Calibration

Phase 4.2 corrected world-space emission, but the demo still looked wrong because `NozzleEmission` initialized too many visible particles at once. The result was a large colored cloud instead of a controlled stream from the bucket nozzle.

Phase 4.3 adds active particle lifecycle data to the GPU particle state:

- `active`
- `age`
- `lifetime`
- `seed`

Lifecycle behavior:

- `FluidBox` and `InternalBucketFluid` initialize particles as active.
- `NozzleEmission` initializes the allocated buffer as inactive.
- `EmitFromNozzle` activates particles from a GPU ring buffer.
- Activated particles receive nozzle position, inherited bucket velocity, nozzle velocity, spread, age `0`, and a configurable lifetime.
- Inactive particles are skipped by `BuildGrid`, `ComputeDensityPressure`, `ComputeForces`, `Integrate`, and collision kernels.
- Inactive particles are clipped by the particle render shader, so allocated but unused buffer slots do not become a visible cloud.
- Only compact counters are read back: active count, inactive count, total emitted count, and grid overflow count.

The default Phase 4 demo profile is now `Profile_BucketNozzleDemo`:

- 20,000 allocated particles.
- Render stride `1`.
- Small particle radius and smoothing length for a readable nozzle stream.
- Open-world/root bounds sized around the swinging bucket.

The Phase 4 scene is calibrated for a narrow paint-like stream:

- Default mode: `NozzleEmission`.
- Default profile: `BucketNozzleDemo`.
- Particle size: small billboard particles instead of large blobs.
- Default color: orange/red paint.
- Emission rate: moderate, with short particle lifetime.
- Legacy CPU paint UI is hidden so the viewer does not confuse CPU paint particle counts with GPU SPH counts.

Testing:

- Press Play in `MainSimulationScene_Phase04_BucketSphNozzle.unity`.
- Press `2` and `R` to restart the nozzle stream.
- Press `1` and `R` to test internal bucket-local fluid.
- Press `F3` to test the Professor 1M profile; render stride remains supported.
- Press `E` to toggle nozzle emission.

`InternalAndEmission` remains postponed. Correct simultaneous internal bucket-local SPH and external world-space emission should use a two-solver or mixed-domain architecture later. Phase 4.3 does not fake that feature.

Canvas impacts and canvas painting are still Phase 5 work.

## Phase 4.4 Finite Paint Reservoir and Internal Fluid Visual

Phase 4.4 was needed because Phase 4.3 produced a readable nozzle stream, but the bucket still looked empty and the nozzle behaved like an infinite emitter. That made the demo visually misleading: paint appeared from the nozzle without any finite source inside the bucket.

`BucketPaintReservoir` adds a small, independent paint amount model for the bucket SPH demo:

- `maxPaintAmount` and `initialPaintAmount` define the refillable bucket capacity.
- `remainingPaintAmount` is reduced only when actual GPU particles are emitted.
- `paintAmountPerParticle` maps emitted particle count to consumed reservoir amount.
- `fillPercent` reports the visible fill level.
- `allowInfiniteDebugEmission` exists only as an explicit debug override and is off by default.

Finite emission now works through `BucketSphNozzleEmitter`:

- The emitter calculates its requested particles for the frame from emission rate, `drainRateMultiplier`, and a fill-percent flow factor.
- The reservoir clamps that request to the paint still available.
- Only the actual emitted particle count is sent to `GpuSphSolver.EmitFromNozzle`.
- Only the actual emitted particle count consumes paint.
- When the reservoir is empty, no new particles are activated. Already emitted particles continue until lifetime or bounds deactivate them.

The default drain uses `Profile_BucketNozzleDemo`, `NozzleEmission`, 20,000 allocated particles, approximately 650-850 particles per second, 0.001 paint amount per particle, and a 5-unit reservoir. The square-root fill factor slows the stream near empty, giving a visible finite drain rather than an abrupt cutoff.

`BucketInternalFluidVisual` adds the visible paint inside the bucket. It is intentionally a reservoir visualization, not true SPH surface reconstruction:

- It renders one simple transparent circular surface inside the bucket.
- Its height follows `BucketPaintReservoir.FillPercent`.
- It follows the bucket transform and uses bucket motion to add a small clamped slosh/tilt visual.
- It does not create particle GameObjects.
- It does not affect SPH physics or canvas painting.

The Phase 4 scene now uses paint-colored materials:

- `MAT_BucketInternalPaint` for the internal reservoir surface.
- `MAT_BucketNozzlePaintParticle` for the GPU nozzle particles.

`BucketSphDebugUI` now identifies the demo as `Bucket SPH Phase 4.4` and shows:

- mode and profile
- simulated, active, rendered, and render-stride counts
- requested and actual emitted particles per frame
- remaining paint amount and fill percentage
- reservoir status: `FULL`, `DRAINING`, `LOW`, `EMPTY`, or `INFINITE DEBUG`
- last consumed paint amount
- nozzle world position, bucket velocity, grid overflow, and GPU memory

Controls:

- `R`: reset SPH and refill the reservoir.
- `E`: toggle emission without refilling.
- `P`: add a small amount of paint for debug inspection.
- `I`: toggle explicit debug infinite emission. This is off by default and labeled as `INFINITE DEBUG`.
- `1`: internal bucket fluid mode.
- `2`: finite nozzle emission mode.
- `F1`: bucket nozzle demo profile.
- `F2`: presentation profile.
- `F3`: Professor 1M profile.
- `F4`: VR safe profile.

Known limitations:

- The internal surface is a visual reservoir surface, not SPH surface extraction.
- `InternalAndEmission` remains postponed until a two-domain or two-solver design is implemented.
- The finite reservoir is a demo control model and not a full volume-conserving SPH source.
- Canvas painting, splats, impact extraction, and GPU-to-canvas bridge work remain Phase 5.

## Phase 5 Focus

Phase 5 should add sparse GPU impact extraction and a controlled bridge from SPH impacts to canvas painting. That work should avoid full particle readback, avoid CPU particle loops, and keep legacy painting available until visual parity is proven.
