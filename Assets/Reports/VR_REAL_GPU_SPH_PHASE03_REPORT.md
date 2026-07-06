# VR Real GPU SPH — Phase 03 Core Upgrade

## What changed

This phase upgrades the GPU paint simulation from a preview-only particle stream into a real SPH-ready GPU core.
The new core keeps particle data on the GPU and introduces a bounded uniform-grid neighbor structure.

## Added / upgraded systems

- `RealGpuSphController.cs`
  - Phase 03 runtime mode.
  - GPU particle buffer.
  - GPU render buffer.
  - GPU uniform-grid cell counters.
  - GPU per-cell particle index slots.
  - Million-particle mode through `F11`.
  - Phase 03 readout in the stats UI.

- `RealBucketSPH_Phase02.compute`
  - Kept under the original resource name to preserve Unity references.
  - Now contains Phase 03 kernels:
    - `CSInitializeParticles`
    - `CSClearGrid`
    - `CSBuildGrid`
    - `CSComputeDensityPressure`
    - `CSIntegrateSph`
    - `CSGenerateRenderParticles`

## Solver design

The solver uses a GPU uniform grid rather than CPU-side particle loops:

1. Clear grid cell counters.
2. Insert every particle into a GPU cell.
3. Compute density and pressure by scanning bounded neighbor cells.
4. Integrate pressure, viscosity, gravity, slosh acceleration, cohesion and surface tension.
5. Apply bucket walls, bucket bottom, rim-pouring logic and surface-plane deposition.
6. Generate a strided render buffer for indirect drawing.

## Why this matters

The old CPU fallback must never be responsible for a million particles. In this phase:

- Particles remain inside `ComputeBuffer` objects.
- Rendering uses `Graphics.DrawMeshInstancedIndirect`.
- Neighbor queries are bounded by `maxParticlesPerCell`.
- The CPU only controls parameters and reads high-level status.

## Controls

- `F9`: toggle GPU SPH runtime.
- `F11`: enable million-particle mode.
- `F12`: reset GPU fluid.

## Recommended test path

1. Open Unity.
2. Clear Console.
3. Run `Tools > VR Paint > SPH Phase 03 > Run Phase 03 Validation`.
4. Enter Play Mode.
5. Press `F9`.
6. Wait several seconds.
7. Press `F11` only after the preview mode is stable.
8. Watch FPS and Console.

## Important note

This phase is a strong GPU SPH core foundation, not the final cinematic liquid surface pass.
The next phase should add impact readback / append-buffer batching from GPU SPH into `PaintImpactEngineV2`, then improve the liquid surface shader and bucket fill visualization.
