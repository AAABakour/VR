# VR Fluid Film Upgrade Notes

## Important correction
The raised mesh paint system has been disabled. The canvas paint is now treated as a **surface liquid film**, not as a generated mesh.

## What changed

### 1. New surface-fluid solver
Added:

- `Assets/ImpactV2/PaintFilmFluidSolverV2.cs`

This solver treats paint on the canvas as a 2D liquid height field with:

- thickness / volume per cell
- wetness per cell
- stored surface velocity per cell
- pressure-driven spreading
- gravity-driven downhill motion over the tilted board
- viscosity
- adhesion/friction
- surface tension / cohesion
- capillary spread

This is closer to a physically meaningful liquid-film model for paint on a board than the old random drip transfer.

### 2. No raised mesh geometry
The old `PaintThicknessRendererV2` component is disabled in `MainSimulationScene`:

- `enableRaisedPaintMesh = false`
- component disabled
- `PaintSurfaceStateV2.autoCreateRaisedPaintRenderer = false`

The paint no longer relies on fake raised geometry.

### 3. Fluid texture + normal-map rendering
`CanvasPainter` now composites the paint from the surface fluid map:

- base stains from impact splats stay in a stain buffer
- fresh liquid is rendered from the live thickness/wetness map
- thickness is visible through color density, wet highlights, and a generated normal map
- wet regions look more glossy, thick regions look darker and heavier

This gives visible thickness without building a mesh.

### 4. Legacy drip solver disabled
The old `PaintDripSolverV2` remains in the project for comparison but is disabled in the main scene. The new fluid-film solver is the active system.

## Main runtime values

Default values in `MainSimulationScene`:

- solver interval: `0.035`
- substeps: `3`
- viscosity: `5.5`
- pressure strength: `2.4`
- gravity strength: `2.6`
- surface tension: `0.42`
- adhesion: `0.34`
- wet friction: `0.58`

## Recommended testing

1. Open `MainSimulationScene`.
2. Press Play.
3. Use `Dense` first because thick blobs show the new fluid behavior clearly.
4. Watch the UI line `Fluid Film`.
5. If paint moves too slowly, lower `viscosity` or increase `gravityStrength`.
6. If paint spreads too much, increase `adhesion` or `wetFriction`.
7. If blobs break apart too much, increase `surfaceTension`.
