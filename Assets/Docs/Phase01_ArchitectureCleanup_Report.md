# Phase 1 Architecture Cleanup Report

Project: SwingingPaintBucketSimulation

Date: 2026-06-29

## 1. Current Project State

The project currently has a working prototype scene at:

- `Assets/MainSimulationScene.unity`

The scene includes a pendulum-driven bucket, a simple rope cylinder, a legacy paint emitter, a CPU droplet simulator, canvas painting, impact processing, debug UI, and reset/export controls. Phase 1 preserves this scene and avoids changing its serialized references.

The requested `Assets/Scripts/Paint/FluidSurfaceVisualizer.cs` file was not present in the project at the time of this cleanup. No replacement was created in Phase 1.

## 2. Existing Systems

- `Assets/Scripts/Physics/PendulumController.cs`
  - Drives the current bucket swing.
  - Positions the bucket from a pendulum approximation.
  - Updates the current rope cylinder visual.
  - Exposes `ResetSimulation()` and `GetBucketVelocity()`.

- `Assets/Scripts/Paint/PaintEmitter.cs`
  - Emits legacy paint droplets from the nozzle.
  - Contains current internal slosh, flow, nozzle shape, and paint amount logic.
  - Exposes `ResetEmitter()` and read-only paint state properties.

- `Assets/Scripts/Paint/PaintParticleSimulator.cs`
  - Simulates a limited CPU droplet system.
  - Handles droplet motion, pooling of visual droplets, lightweight particle interaction, and canvas impacts.
  - Exposes `ResetParticles()` and active particle counters.

- `Assets/Scripts/Paint/CanvasPainter.cs`
  - Owns the current runtime canvas texture.
  - Handles paint impact mapping, splats, spray, directional smear, and canvas reset.
  - Exposes coverage and painted-area utilities.

- `Assets/Scripts/Core/SimulationManager.cs`
  - Owns the current `R` key reset flow.
  - Resets the canvas, pendulum, and paint emitter.

- `Assets/Scripts/UI/SimulationStatsUI.cs`
  - Displays FPS, active particles, visible droplets, paint remaining, surface type, and nozzle shape.

## 3. Legacy / Fallback Systems

The current pendulum, paint emitter, CPU droplet simulator, and canvas painter remain active prototype systems. They are preserved as legacy/fallback systems until future phases replace them with higher-quality architecture.

Legacy/fallback today:

- `PendulumController`
- `PaintEmitter`
- `PaintParticleSimulator`
- `CanvasPainter`
- `SimulationManager` reset flow
- `SimulationStatsUI`

Phase 1 adds adapters rather than rewriting these systems:

- `LegacyPaintSystemAdapter`
- `LegacyPendulumAdapter`

These adapters let the future lifecycle manager control current systems when a future integration scene chooses to wire them in.

## 4. New Folder Structure

Created or confirmed:

- `Assets/Scripts/Core/Architecture`
- `Assets/Scripts/Core/Profiles`
- `Assets/Scripts/Core/Validation`
- `Assets/Scripts/RopeRig`
- `Assets/Scripts/Bucket`
- `Assets/Scripts/SPH`
- `Assets/Scripts/SPH/Rendering`
- `Assets/Scripts/SPH/Collision`
- `Assets/Scripts/SPH/Integration`
- `Assets/Scripts/Painting`
- `Assets/Scripts/UI/Debug`
- `Assets/Scripts/Legacy`
- `Assets/Docs`
- `Assets/Scenes/Integrated`
- `Assets/Scenes/Benchmarks`

Existing working scripts were intentionally left in their current folders to avoid breaking Unity script references.

## 5. New Architecture Scripts

- `ISimulationSubsystem`
  - Defines the minimal lifecycle contract for reset, pause, and profile application.

- `SimulationLifecycleManager`
  - Future-facing manager that can coordinate registered subsystem adapters.
  - Supports explicit subsystem assignment and optional discovery in children or the scene.
  - Safe when no subsystems are present.

- `SimulationMode`
  - Defines shared mode names for future profile-driven workflows.

- `SimulationProfile`
  - ScriptableObject definition for target particle count, render stride, impact budget, subsystem toggles, debug visuals, and notes.
  - Does not depend on a GPU SPH implementation.

- `SceneReferenceValidator`
  - Runtime-friendly validator for expected scene objects and key component references.
  - Logs one clear summary when run instead of checking every frame.

- `LegacySystemTag`
  - Simple marker component for documenting legacy objects in future scene passes.

- `LegacyPaintSystemAdapter`
  - Wraps current `PaintEmitter`, `PaintParticleSimulator`, and `CanvasPainter`.
  - Implements `ISimulationSubsystem`.

- `LegacyPendulumAdapter`
  - Wraps current `PendulumController`.
  - Implements `ISimulationSubsystem`.
  - Exposes the current bucket velocity for future bridge code.

## 6. Why Phase 1 Avoids Implementing SPH

GPU SPH needs clear ownership boundaries, data-flow contracts, rendering strategy, collision/event limits, and benchmark scenes before solver implementation. Adding solver code before those decisions would risk creating another tightly coupled prototype path.

Phase 1 therefore creates names, folders, profiles, lifecycle contracts, adapters, and documentation only. No GPU compute solver, million-particle system, final chain rig, or final paint bridge was implemented.

## 7. Planned Next Phases

- Phase 2: Rope/Chain Rig + Bucket Rig
  - Build a real rig boundary for rope/chain and bucket motion.
  - Keep the current pendulum available as a fallback until the rig is verified.

- Phase 3: Independent Fluid Box GPU SPH Benchmark
  - Create a benchmark scene under `Assets/Scenes/Benchmarks`.
  - Implement and validate a standalone GPU fluid box before connecting it to the bucket.

- Phase 4: Bucket Internal Fluid + Nozzle Emission
  - Integrate internal fluid representation with bucket motion.
  - Replace legacy slosh/nozzle approximations only after the benchmark solver is stable.

- Phase 5: External Falling Paint GPU SPH
  - Extend the solver to emitted paint flow outside the bucket.
  - Avoid GameObject-per-particle designs.

- Phase 6: Collision/Impact Bridge + Canvas Painting
  - Convert fluid impact data into bounded canvas events.
  - Keep impact budgets and validation visible through profiles.

- Phase 7: UI/Visual Polish
  - Improve debug/presentation UI after the simulation architecture is proven.
  - Add VR-safe presentation settings through profiles.

## 8. How To Open And Test The Current Scene

1. Open `Assets/MainSimulationScene.unity`.
2. Enter Play Mode.
3. Confirm the bucket swings, paint emits, droplets hit the canvas, and the debug UI updates.
4. Press `R` and confirm the existing reset behavior still works.

This scene was not moved or rewritten in Phase 1.

## 9. How To Open The Phase 1 Integration Scene

1. Open `Assets/Scenes/Integrated/MainSimulationScene_Phase01_Architecture.unity`.
2. Enter Play Mode.
3. Confirm it behaves like the original scene because it is a safe duplicate.
4. Add `SceneReferenceValidator` to a temporary scene object if you want to run reference validation from the component context menu.

The duplicated scene has a unique `.meta` GUID and does not reuse the original scene meta file.

## 10. Known Risks

- The future architecture scripts are not wired into the current scene by default. This is intentional to preserve behavior.
- `FluidSurfaceVisualizer.cs` was listed in the request but not present in the project.
- Empty future folders are prepared but do not yet contain production systems.
- The legacy CPU particle simulator is not representative of the future GPU SPH performance target.
- Profile toggles define intent only; they do not imply a completed GPU solver or rope rig.
- Unity should regenerate `.meta` files for newly added scripts and documentation that do not already have explicit meta files.

## 11. Phase 3.1 Finalization Notes

Phase 3.1 verified that the Phase 1 architecture remains usable as a foundation:

- `SimulationLifecycleManager` still supports explicit subsystem lists and safe child/scene discovery.
- `SceneReferenceValidator` now has separate toggles for legacy, Phase 2 rig, and Phase 3 fluid-box validation so each scene can validate only the systems it owns.
- The original `SimulationManager` and `R` reset behavior remain preserved.
- The required simulation profiles still live in `Assets/Settings/SimulationProfiles`.

Phase 1 remains a compatibility layer, not a replacement of the original prototype scene.
