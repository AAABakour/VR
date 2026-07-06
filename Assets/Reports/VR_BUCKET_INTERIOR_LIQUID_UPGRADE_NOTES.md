# VR Bucket Interior Liquid Upgrade

## Goal
This upgrade makes the bucket visibly contain interactive paint. The paint level is directly driven by the real emitter amount, so the liquid surface decreases while the bucket pours and reaches empty when the simulation runs out of paint.

## Added System
`Assets/Scripts/Paint/BucketInteriorLiquidSystemV2.cs`

Runtime features:
- Auto-creates itself at scene load, so no manual setup is required.
- Finds the `Bucket` object and `PaintEmitter` automatically.
- Builds procedural liquid geometry inside the bucket:
  - gravity-aligned liquid free surface,
  - translucent internal paint volume shell,
  - glossy meniscus / wet rim band.
- Reads `PaintEmitter.PaintFill01` and `RemainingPaintAmount` every frame.
- Uses the emitter paint color automatically.
- Includes a slosh model driven by bucket acceleration.
- Creates a presentation X-Ray bucket transparency mode so the internal paint remains visible even if the bucket model is visually opaque.

## PaintEmitter Changes
`PaintEmitter` now exposes:
- `CurrentFlowRate`
- `LastEmittedParticleCount`
- `IsPaintEmpty`
- `SloshIntensity01`
- `SetFill01(float)`
- `SetRemainingPaintAmount(float)`
- `PreserveFillWhenChangingCapacity(float)`
- `RefillToInitialAmount()`

This allows the UI and bucket liquid visualization to control and read the actual paint amount cleanly.

## Modern UI Changes
The Modern Game UI now has a new section:

`BUCKET INTERNAL PAINT`

Controls added:
- Show Interior Paint
- Transparent Bucket View
- Gravity-Level Surface
- Liquid Volume Shell
- Meniscus / Rim Wetness
- Interior Radius
- Bottom Level
- Rim Level
- Surface Quality
- Surface Rings
- Slosh Strength
- Tilt Response
- Wave Amplitude
- Wave Frequency
- Liquid Gloss
- Bucket X-Ray Alpha

A new telemetry card shows:
- bucket liquid status,
- fill percentage,
- remaining liters,
- current flow rate,
- slosh intensity.

## Recommended Tuning
If the liquid is not perfectly aligned with the bucket model, tune these from the UI:
- `Interior Radius`
- `Bottom Level`
- `Rim Level`

If the liquid is not visible enough:
- enable `Transparent Bucket View`,
- lower `Bucket X-Ray Alpha` to around `0.45 - 0.65`.

If the liquid is too calm:
- increase `Slosh Strength`,
- increase `Wave Amplitude`,
- increase `Tilt Response`.

If the liquid is too noisy:
- decrease `Wave Amplitude`,
- decrease `Slosh Strength`,
- increase emitter viscosity.
