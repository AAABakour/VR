# VR Paint SPH Phase 04B — Visual Rebuild Hotfix

## Why this patch exists
Phase 04 proved that the GPU SPH core can run, but the raw visual result was still too noisy: the paint appeared as sparse red particles, the falling stream looked like a thin dotted line, and the board impact looked too speckled. Phase 04B keeps the GPU solver alive but separates **simulation particles** from **presentation visuals**.

## Main changes
- Raw GPU particle rendering is disabled by default. The particles still simulate on GPU, but they are no longer the primary visual representation.
- Added `SphHeroPaintVisualRenderer`:
  - smooth cohesive Bezier tube for the falling paint stream.
  - glossy impact pool on the board.
  - controlled hero droplets around the stream.
  - runtime auto-detection of bucket, canvas, paint emitter, and GPU SPH controller.
- Added `SphHeroPaintSurface_URP.shader` for darker, glossier, thicker paint.
- Added `SphVisualQualityStabilizer`:
  - reduces legacy CPU spray noise.
  - lowers random spread and tiny particle spam.
  - makes the paint darker and less neon.
  - limits canvas speckle and increases smear continuity.
- Adjusted SPH default values for heavier paint:
  - higher viscosity/cohesion.
  - lower spill push.
  - lower max particle velocity.
  - hidden contained/deposited raw particles.
- Adaptive quality no longer auto-downgrades immediately in the Editor, so visuals do not collapse to LowResource during warmup.

## Expected result
The project should now look less like a point-cloud debug view and more like a controlled paint simulation:
- contained paint appears as a volume/surface in the bucket.
- pouring appears as a smooth, viscous stream.
- droplets are secondary accents, not the main paint body.
- the impact area appears more like a wet paint pool and less like random red dust.

## Test steps
1. Clear Console.
2. Enter Play Mode.
3. Press `F9`.
4. Tilt/swing the bucket or use scene presets.
5. Check that the previous dotted vertical line is no longer the dominant visual.
6. Test `F11` only after the visual preview is stable.

## Notes
This is an art-directed reconstruction layer over the GPU SPH data. The next step should connect GPU collision accumulation to the paint-impact engine so the board marks are driven by the SPH stream instead of the older legacy particle emitter.
