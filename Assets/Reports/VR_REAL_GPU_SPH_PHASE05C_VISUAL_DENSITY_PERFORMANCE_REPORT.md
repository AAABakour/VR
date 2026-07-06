# VR Real GPU SPH — Phase 05C Visual Density + Performance Rebuild

This phase continues from the stable Phase 05 / Phase 05B line and deliberately avoids the unstable Phase 06 branch.

## Goal

The user feedback was that the droplets were too slow, too few, visually did not communicate the 1,000,000 particle target, and FPS was too low. Phase 05C separates **simulation density** from **presentation density**:

- The GPU solver remains available for high-scale SPH.
- Raw GPU points are hidden by default because they look like debug dust and are expensive to render.
- The visible pour is reconstructed as a fast cohesive stream plus dense instanced droplets.
- The project avoids CPU-per-droplet GameObjects for the dense layer.

## Added

- `SphPhase05CVisualDensityPerformanceDirector.cs`
- `SphPhase05CInstancedDroplet_URP.shader`
- `Tools > VR Paint > SPH Phase 05C > Run Phase 05C Validation`

## Main Improvements

1. Faster falling droplets through a higher visual fall speed.
2. More visible droplets through GPU instancing rather than CPU objects.
3. Larger cohesive stream radius so the pour reads as heavy paint.
4. Lower-cost SPH presentation defaults: one substep, smaller grid, high render stride, hidden raw points.
5. FPS protection in the editor: if full 1,000,000-particle mode drops the editor too hard, Phase 05C keeps the perceived million-particle visual layer active while reducing the live solver budget for presentation.

## Recommended Test Order

1. Open Unity.
2. Clear Console.
3. Run: `Tools > VR Paint > SPH Phase 05C > Run Phase 05C Validation`.
4. Press Play.
5. Press `F9` only.
6. Evaluate visual density and FPS.
7. Press `F11` only after the F9 presentation mode is stable.

