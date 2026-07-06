# VR Final SPH-Ready Upgrade Report

## What changed

This version prepares the project for a high-scale GPU SPH fluid system without breaking the current bucket, rope, UI, impact, and canvas-painting systems.

The upgrade adds:

1. `SphParticleBudgetController` for the 1,000,000+ particle target.
2. `SphCollisionSurfaceBridge` for batched SPH-to-canvas impact forwarding.
3. `GpuSphSolverBridge` as the runtime bridge for the next GPU compute solver stage.
4. `BucketSPH_Ready.compute` as the compute-shader skeleton for bucket particles, grid building, gravity, and bucket collision boundaries.
5. `HighScalePaintFluid_URP.shader` as a transparent wet-paint preview shader for the future GPU fluid renderer.
6. `RuntimeAdaptiveQualityGovernor` to protect FPS during demonstrations.
7. Stronger QA checks for SPH readiness and performance safety.
8. A Unity Editor menu item: `Tools / VR Paint / Run Final Readiness Scan`.

## Main design decision

The existing CPU droplet system is now treated as a visual/legacy fallback. It must not be used to simulate one million particles. For the final SPH phase, the million-particle fluid must run in GPU buffers and only send compressed/batched collision data back to the CPU painting pipeline.

## Final target architecture

- Bucket liquid volume: GPU SPH particles inside the bucket.
- Airborne paint: GPU particle stream emitted from the nozzle.
- Collision: GPU broad phase + CPU batched impact bridge.
- Canvas deposition: current `PaintImpactEngineV2`, `PaintSurfaceStateV2`, and `PaintFilmFluidSolverV2` remain the receiving surface pipeline.
- Rendering: render stride/instancing so only a controlled visible subset is drawn.
- UI/QA: current modern UI remains intact and now includes the ultra SPH preparation profile.

## Performance policy

For a 1,000,000-particle target:

- CPU fallback particles are limited to hundreds, not millions.
- SPH particle data is estimated at roughly 96 bytes per particle.
- Collision hits are batched and capped per frame.
- Canvas texture and film updates are throttled.
- Adaptive quality can downgrade automatically if FPS stays low.

## Recommended next step

The next stage should replace the placeholder GPU bridge with the full SPH pipeline:

1. GPU particle emission from nozzle.
2. Uniform grid / spatial hash with prefix sums or GPU sorting.
3. Density and pressure kernels.
4. Viscosity, surface tension, adhesion.
5. Bucket SDF collision.
6. Canvas collision extraction into append buffers.
7. Indirect instanced rendering or VFX Graph renderer.
8. Optional screen-space fluid smoothing for the final look.
