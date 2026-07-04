# VR Paint Collision + Thickness Upgrade

## Upgrade scope
This upgrade focuses on the user's section: paint collision, paint accumulation on the board, wet paint look, drips, and visible physical thickness.

## Major changes

### 1. Exact surface collision
`PaintParticleSimulator` no longer checks only the global Y value of the board. It now resolves collision against the real canvas plane using the board transform and surface normal. This makes impacts more accurate when the board is rotated or tilted.

### 2. Bounds-aware impact filtering
Particles that cross the board plane outside the actual canvas area are rejected instead of painting outside the board.

### 3. Raised paint thickness mesh
Added `PaintThicknessRendererV2`.

The renderer reads the `PaintSurfaceStateV2` thickness/wetness maps and generates a runtime raised mesh above the painted regions. This gives the paint actual visible thickness instead of a flat texture only.

Important controls:
- `meshResolution`: visual detail of the raised paint mesh.
- `maxHeightWorld`: maximum visible paint height.
- `thicknessSensitivity`: how quickly thickness becomes visible.
- `visibleThicknessCutoff`: removes almost-empty triangles so the whole board does not become red.
- `smoothing`: stabilizes mesh changes and avoids noisy flickering.

### 4. Improved mass deposition
`PaintSurfaceStateV2` now deposits paint with a thicker center and a subtle raised rim, closer to real viscous paint splats.

### 5. Wet paint relief shading
`CanvasPainter` now adds subtle center darkening, raised rim highlight, and pigment variation to make paint look less flat even before the raised mesh is noticed.

### 6. More stable drip behavior
`PaintDripSolverV2` now slows very thick paint slightly and improves gravity trail coherence so drips form more believable downward trails instead of noisy random diffusion.

### 7. Cleaner reset
`SimulationManager` now resets particles, canvas texture, impact stats, surface maps, drip solver, and the raised paint renderer together.

### 8. UI stats
`SimulationStatsUI` now displays raised paint mesh status, patch count, and rendered height.

## Recommended first test
1. Open `Assets/MainSimulationScene.unity`.
2. Press Play.
3. Use `Dense` first because it produces visible thickness fastest.
4. Look for the new UI line: `Raised Paint: ON`.
5. If the paint looks too thick, lower `maxHeightWorld` on `PaintThicknessRendererV2`.
6. If thickness is too subtle, raise `thicknessSensitivity` or lower `visibleThicknessCutoff`.

## Notes
The raised mesh is created at runtime by `PaintSurfaceStateV2`, so the scene does not require manual dragging. If needed, add `PaintThicknessRendererV2` directly to `CanvasBoard` to expose all settings before Play.
