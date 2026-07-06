# VR Real GPU SPH - Phase 04 Visual Volume Reconstruction

## Purpose
Phase 04 upgrades the visible liquid layer so the paint no longer looks like a raw point cloud. The GPU particles still drive the simulation, but contained paint is reconstructed as a smooth, gravity-aware liquid volume inside the bucket.

## Added Systems
- `SphFluidVolumeVisualRenderer.cs`
  - Auto-created at runtime.
  - Follows the real bucket transform.
  - Builds a dynamic liquid surface, side body, and meniscus/rim highlight.
  - Uses acceleration and gravity to tilt the surface and create slosh waves.
  - Uses bounded mesh budgets so the visual layer stays cheap.
- `SphFluidVolumeSurface_URP.shader`
  - Transparent URP paint shader.
  - Fresnel/specular highlight.
  - Subtle depth darkening and slosh sparkle.
- Phase 04 parameters in `RealGpuSphController.cs`
  - Contained GPU particles are softened visually.
  - Airborne particles are enlarged into droplets/blobs.
  - Deposited particles are rendered smaller so surface paint stays readable.

## Why this architecture
Rendering one million particles directly as visible balls inside the bucket creates noisy visuals and poor realism. Production-style fluid presentations usually separate simulation data from final visual reconstruction. This phase keeps physics on the GPU, while the visible mass is reconstructed into a coherent liquid volume.

## User Controls
- `F9`: toggle GPU SPH runtime.
- `F11`: switch to million-particle mode.
- `F12`: reset GPU fluid.
- Unity menu: `Tools > VR Paint > SPH Phase 04 > Run Phase 04 Validation`.

## What to inspect
1. Press Play.
2. Clear the console.
3. Press F9.
4. The bucket should now show a smoother contained liquid surface instead of only particle dots.
5. Swing the bucket and watch the visual surface tilt/slosh.
6. Press F11 only after F9 is stable.

## Next phase
Phase 05 should focus on collision-quality: GPU-to-CPU impact batching, better splat generation, and using density/velocity to generate realistic paint marks on the canvas.
