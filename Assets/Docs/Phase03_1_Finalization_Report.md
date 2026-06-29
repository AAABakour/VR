# Phase 3.1 Finalization Report

## Scope

Phase 3.1 finalizes Phases 1, 2, and 3 as stable foundations. It does not start Phase 4 and does not connect SPH to the bucket, nozzle, or canvas.

## Phase 1 Fixes

- `SceneReferenceValidator` now has separate validation toggles for legacy scene references, Phase 2 rig references, and Phase 3 fluid-box references.
- Phase 1 and Phase 2 integration scenes explicitly configure those toggles.
- The original `SimulationManager` and `R` reset flow remain untouched.
- Simulation profiles remain in `Assets/Settings/SimulationProfiles`.

## Phase 2 Fixes

- `RopeRigSubsystemAdapter` and `BucketRigSubsystemAdapter` are present and implement `ISimulationSubsystem`.
- Phase 2 scene wires both adapters into `SimulationLifecycleManager`.
- `BucketCollisionProxy` supports explicit `nozzlePoint` and still falls back to `nozzleLocalPosition`.
- Execution order is set for motion provider, handle rig, and rope rig.
- The old rope cylinder remains disabled in the Phase 2 scene only.

## Phase 3 Fixes

- `GpuSphSolver` now treats `GpuSphSettings_FluidBox` as a template and applies profiles to a runtime clone.
- `MAT_GpuSphParticle` is assigned explicitly to the benchmark renderer.
- `MAT_TransparentFluidBox` is assigned explicitly to the transparent box controller.
- GPU grid overflow is counted in a compact counter buffer.
- The UI displays overflow count, max particles per cell, memory estimates, dispatch groups, solver state, and buffer validity.
- Full particle CPU readback is still avoided. Only the compact overflow counter is read periodically.

## Scene List

- `Assets/MainSimulationScene.unity`: original legacy prototype scene.
- `Assets/Scenes/Integrated/MainSimulationScene_Phase01_Architecture.unity`: architecture wiring duplicate.
- `Assets/Scenes/Integrated/MainSimulationScene_Phase02_RopeBucketRig.unity`: rope/bucket rig integration duplicate.
- `Assets/Scenes/Benchmarks/FluidBoxBenchmarkScene.unity`: independent transparent fluid box GPU SPH benchmark.

## Current System Status

- Architecture: lifecycle manager, subsystem interface, profiles, validation, and legacy adapters are ready.
- Legacy systems: preserved and tagged/documented where useful.
- Rope rig: visual rope/chain foundation, lifecycle adapter, no final physics constraint yet.
- Bucket rig: reference and metadata foundation, handle motion, collision/nozzle proxy.
- GPU SPH fluid box: independent benchmark with GPU buffers, bounded grid neighbor search, SPH forces, box collision, and GPU rendering.
- UI/debug: benchmark UI reports counts, memory, stride, overflow, solver state, FPS, and profile mode.

## SPH Honesty

- Simulated particles are stored in GPU particle buffers.
- Rendered particles are reduced by render stride.
- ProfessorBenchmark uses 1,000,000 simulated particles with render stride 10, producing about 100,000 rendered particles.
- The current grid is bounded by `maxParticlesPerCell`.
- If cells overflow, the UI reports `Grid overflow count`.
- Overflow does not mean the particle buffer lost particles; it means neighbor-grid participation was truncated for that frame.
- A sorted grid or prefix-sum compact grid is still future work.

## How To Test

Original scene:

1. Open `Assets/MainSimulationScene.unity`.
2. Press Play.
3. Confirm swinging bucket, paint emission, canvas painting, UI, and `R` reset.

Phase 2 scene:

1. Open `Assets/Scenes/Integrated/MainSimulationScene_Phase02_RopeBucketRig.unity`.
2. Press Play.
3. Confirm rope/chain rig follows `RopeAttachPoint`, handle motion is stable, and legacy rope visual is not fighting the rig.

Fluid box benchmark:

1. Open `Assets/Scenes/Benchmarks/FluidBoxBenchmarkScene.unity`.
2. Press Play.
3. Confirm transparent box, visible GPU particles, UI stats, and motion controls.

Professor 1M benchmark:

1. In the fluid box scene, press `Professor 1M`.
2. Confirm UI reports 1,000,000 simulated particles.
3. Confirm rendered particles are lower because render stride is 10.
4. Watch `Grid overflow count` and FPS to understand hardware and bounded-grid limits.

## Ready For Phase 4

Ready:

- Stable architecture/lifecycle boundary.
- Rope/bucket references and nozzle/collision metadata.
- Independent GPU SPH foundation.
- Explicit materials and settings template.
- Visible debug UI and honest overflow reporting.

Phase 4 must not assume:

- The bounded grid is a final production neighbor-search implementation.
- SPH is already integrated with the bucket.
- Nozzle emission exists.
- Canvas impact extraction exists.
- Rendered particle count equals simulated particle count.

## Known Limitations

- No bucket internal SPH yet.
- No nozzle emission yet.
- No SPH-to-canvas bridge yet.
- No final surface reconstruction.
- Bounded grid overflow can reduce SPH quality in dense configurations.
- ProfessorBenchmark mode is hardware-heavy.

## Recommended Phase 4 Focus

- Convert `BucketCollisionProxy` data into bucket-local SPH bounds.
- Add nozzle emission without full particle readback.
- Replace bounded grid slots with compact sorted cell ranges.
- Extract sparse impact events for canvas painting.
- Keep legacy paint active until SPH parity is verified.
