# VR Real GPU SPH - Phase 05 Collision + Impact Quality

## Goal
Phase 05 upgrades the visual and physical handoff between the GPU SPH paint stream and the canvas paint-surface system. The previous phases made the liquid core and hero visual layer stable; this phase makes the paint impact behave like cohesive wet paint instead of noisy particle spray.

## Added systems

### SphPhase05ImpactDirector
A runtime macro-impact director that estimates the high-level flow from the bucket rim to the canvas and submits coherent paint hits to the existing `PaintImpactEngineV2` pipeline. This avoids CPU readback of millions of particles while still producing realistic surface deposition.

Key behaviors:
- Detects bucket tilt relative to gravity.
- Estimates pour origin from the bucket rim/downhill side.
- Projects the stream toward the canvas/surface plane.
- Generates controlled cohesive blobs and a small number of satellite droplets.
- Keeps impact count bounded per frame.

### Enhanced SphCollisionSurfaceBridge
The bridge now supports Phase 05 spatial coalescing. It can merge many nearby SPH hit samples into larger cohesive impacts before forwarding to `PaintImpactEngineV2`. This prevents noisy speckle and protects CPU performance.

Key fields:
- `enableSpatialCoalescing`
- `clusterCellSize`
- `maxClusteredImpactsPerFrame`
- `clusteredRadiusGain`
- `velocityWeightedClusters`

### Phase05WetPaintImpactQualityTuner
A runtime tuning layer that adjusts the existing paint-surface stack for a heavier wet-paint presentation:
- Reduced random spray noise.
- Stronger cohesive blobs.
- More controlled halo/specular wet look.
- Safer fluid-film and drip settings.
- Less noisy satellite droplets and filaments.

## Why this architecture
A true 1,000,000 particle SPH system should not send one CPU collision event per particle. Phase 05 uses a hybrid approach:

1. GPU keeps high-scale particle state.
2. Visual layer reconstructs a clean paint stream.
3. Macro-impact director and bridge produce bounded surface impacts.
4. Existing `PaintImpactEngineV2`, `AdvancedSplatGeneratorV2`, `PaintSurfaceStateV2`, and `PaintFilmFluidSolverV2` remain the final surface-paint system.

This keeps the system visually convincing while staying safe for real-time VR/Unity delivery.

## Test procedure
1. Open Unity.
2. Run `Tools > VR Paint > SPH Phase 05 > Run Phase 05 Validation`.
3. Enter Play Mode.
4. Press `F9` to start GPU SPH preview.
5. Swing/tilt the bucket until the stream reaches the canvas.
6. Observe the left stats panel:
   - Phase05 impact director status.
   - Phase05 collision bridge status.
   - Total impacts / surface wetness / fluid film.
7. Press `F11` only after the normal preview is stable.

## Expected result
- Fewer random red speckles.
- Larger cohesive wet blobs on impact.
- More believable splash shape.
- Controlled satellite droplets.
- Wet glossy surface-film response.
- No CPU storm from particle-level collisions.
