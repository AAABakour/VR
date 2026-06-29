# Phase 3 Fluid Box GPU SPH Report

## Implemented Scope

Phase 3 adds an independent GPU SPH benchmark scene for a transparent movable rectangular fluid box:

- `Assets/Scenes/Benchmarks/FluidBoxBenchmarkScene.unity`

This phase does not integrate SPH into the paint bucket, nozzle emission, or canvas painting. The legacy paint stack and Phase 2 rope/bucket rig remain intact.

## Phase 2 Stabilization

Small Phase 2 fixes were added before the SPH benchmark:

- `RopeRigSubsystemAdapter` wraps `RopeRigController` for lifecycle reset, pause, and profile toggling.
- `BucketRigSubsystemAdapter` wraps the bucket rig, handle rig, motion provider, and collision proxy without destructively moving the bucket.
- `BucketCollisionProxy` now supports an optional `nozzlePoint` transform and falls back to `nozzleLocalPosition`.
- Execution order was added for bucket motion, handle motion, and rope rig updates to reduce jitter.
- `SceneReferenceValidator` can optionally validate the new Phase 2 subsystem adapters.

## Why An Independent Transparent Fluid Box

The professor requirement is to demonstrate a real SPH foundation without mixing it into unfinished bucket/nozzle/canvas integration. A transparent box benchmark isolates the hard parts:

- GPU particle state ownership.
- Neighbor search.
- Density, pressure, and viscosity terms.
- Containment collision.
- GPU rendering and render stride.
- Profile-driven particle counts up to 1,000,000 simulated particles.

This gives Phase 4 a stable solver foundation to bridge into the bucket safely.

## GPU SPH Solver

Main files:

- `Assets/Scripts/SPH/GpuSphSolver.cs`
- `Assets/Shaders/SPH/GpuSph.compute`
- `Assets/Scripts/SPH/GpuSphSettings.cs`

The solver owns GPU buffers for:

- Particle state: local position, local velocity, density, pressure.
- Force/acceleration accumulation.
- Grid cell counts.
- Grid particle slots.

The CPU configures buffers and dispatches kernels. It does not loop over all particles each frame and does not read back the full particle set.

## SPH Terms

- Density: estimated from nearby particles using the poly6 smoothing kernel.
- Pressure: computed from density relative to rest density.
- Viscosity: damps relative velocity between nearby particles.
- Smoothing length: radius used for neighbor contribution.
- Rest density: target density for pressure response.
- Particle mass: contribution weight for density and forces.

## Spatial Grid

The compute shader uses a bounded uniform grid:

- `ClearGrid`
- `BuildGrid`
- `ComputeDensityPressure`
- `ComputeForces`

Each particle hashes to a cell in box-local space. Density and force kernels search the 27 surrounding cells. Each cell has a fixed maximum slot count, currently configured as 128 by default.

This is not an O(n^2) all-pairs solver. It is a clear Phase 3 grid foundation. The limitation is that overfull cells truncate neighbor participation; Phase 4 should move toward a sorted grid or prefix-sum compact cell ranges for production-scale accuracy.

## Transparent Box Collision

The solver stores particle positions in box-local space. The transparent box can move and rotate independently. Gravity is transformed into the box's local frame, and approximate box acceleration is included so tilt/shake motion visibly affects the fluid.

Collision is handled against local rectangular bounds using:

- bounds size
- wall offset
- collision damping
- tangent damping
- max velocity clamp

## Rendering And Render Stride

Particles are rendered from the GPU particle buffer using procedural indirect drawing:

- `Assets/Scripts/SPH/Rendering/GpuSphParticleRenderer.cs`
- `Assets/Shaders/SPH/GPU_SPH_Particle_Unlit.shader`

No particle GameObjects are created. The renderer draws camera-facing quads from `SV_VertexID`.

Render stride separates simulation count from draw count:

- 50,000 simulated, stride 1 = about 50,000 rendered.
- 250,000 simulated, stride 5 = about 50,000 rendered.
- 1,000,000 simulated, stride 10 = about 100,000 rendered.

The UI reports both simulated and rendered counts separately.

## Profiles

The benchmark controller mirrors the existing `SimulationProfile` assets:

- Debug: 50,000 particles, render stride 1.
- Presentation: 250,000 particles, render stride 5.
- ProfessorBenchmark: 1,000,000 particles, render stride 10.
- VRSafe: capped into a 50,000 to 150,000 range.

The default `GpuSphSettings_FluidBox` asset starts in Debug mode so the scene opens safely.

## How To Run

1. Open `Assets/Scenes/Benchmarks/FluidBoxBenchmarkScene.unity`.
2. Press Play.
3. Use the benchmark UI buttons:
   - Reset Fluid
   - Debug
   - Presentation
   - Professor 1M
   - VR Safe
   - Toggle Motion
   - Cycle Motion

The scene contains a transparent rectangular box, visible GPU-rendered particles, motion presets, camera, lights, and runtime debug UI.

## Performance Limitations

ProfessorBenchmark mode allocates and simulates 1,000,000 particles. It is intentionally heavy and may not run smoothly on all GPUs. Rendering is reduced with render stride, but the simulated particle count remains 1,000,000.

The current uniform grid is bounded and understandable, not a final production neighbor-search implementation. Overcrowded grid cells may truncate neighbor checks. Final solver quality should improve when Phase 4 adds compact sorted cell ranges.

## Not Implemented Yet

- Bucket internal SPH.
- Nozzle emission.
- Fluid-to-canvas impact bridge.
- Paint material transfer from SPH to canvas.
- Screen-space fluid surface reconstruction.
- Production sorted-grid optimization.

## Recommended Phase 4 Focus

Phase 4 should integrate the solver into the bucket safely:

- Replace the fixed fluid box bounds with `BucketCollisionProxy` data.
- Add nozzle emission and outflow controls.
- Add compact GPU grid ranges for better 1M accuracy.
- Add impact event extraction without full particle readback.
- Bridge sparse GPU impact events into canvas painting.
- Keep legacy paint available until the SPH-to-canvas path is stable.
