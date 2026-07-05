# VR Bucket Interior Liquid Rebuild

This version replaces the previous simple bucket-fill visualization with a stronger hybrid interior-liquid system.

## What changed

- Rebuilt `BucketInteriorLiquidSystemV2` as an advanced interior paint volume.
- The liquid now auto-fits itself to the real visible bucket body mesh at runtime.
- Added real fill depletion coupling to `PaintEmitter`.
- The paint no longer drains asymptotically forever; it reaches a true empty state.
- Added a deeper visual stack:
  - glossy free surface
  - volumetric paint body
  - wet inner-wall coating
  - thick meniscus around the contact line
  - small rim highlights / surface detail patches
- Added stronger slosh response from bucket acceleration and angular velocity.
- Added UI controls:
  - Professional Auto-Fit
  - RECALIBRATE LIQUID TO BUCKET
- The bucket liquid telemetry now shows calibration status.

## Important controls

Open the right UI deck > `BUCKET INTERNAL PAINT`.

Best starting settings:

- `Professional Auto-Fit`: ON
- Press `RECALIBRATE LIQUID TO BUCKET` once after the scene starts.
- `Transparent Bucket View`: ON
- `Bucket X-Ray Alpha`: 0.30 - 0.45
- `Surface Quality`: 144
- `Surface Rings`: 40
- `Slosh Strength`: 0.70 - 1.00
- `Tilt Response`: 0.80 - 1.10
- `Wave Amplitude`: 0.025 - 0.055

## Files changed

- `Assets/Scripts/Paint/BucketInteriorLiquidSystemV2.cs`
- `Assets/Scripts/Paint/PaintEmitter.cs`
- `Assets/Scripts/UI/ModernSimulationGameUI.cs`

## Notes

The script keeps the same class name so existing scene references and UI code still work. This is not a fake static fill plane. It is an interactive runtime liquid volume with fill amount, wet wall, meniscus, slosh, gravity-aligned surface, true depletion, and automatic bucket calibration.
