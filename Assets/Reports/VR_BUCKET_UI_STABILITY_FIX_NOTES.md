# VR Bucket Liquid + UI Stability Fix

## What was fixed

### 1. Bucket no longer becomes transparent by default
The previous internal paint presentation enabled X-Ray bucket transparency automatically. This made the bucket body look like a transparent cylinder and weakened realism.

Now:
- `presentationBucketTransparency` defaults to `false`.
- Presets and Reset force the bucket back to opaque mode.
- X-Ray is optional only from the UI.
- Turning X-Ray off restores bucket materials to opaque rendering.

### 2. Internal paint is clamped inside the bucket
The old auto-fit used raw renderer bounds. On imported FBX bucket models, bounds can include handles, offsets, or helper geometry, which caused the liquid to appear above/outside the bucket.

Now:
- Calibration uses robust mesh percentiles instead of raw full bounds.
- Calibration is clamped using the PaintNozzle as a bottom anchor.
- Liquid center is aligned to the nozzle / bucket visual center.
- The surface height is linear with fill percentage, not SmoothStep, so 78% fill no longer appears almost at the rim.
- The default liquid volume shell is disabled to avoid a fake cylinder outside the bucket.

### 3. UI buttons are safer
The modern game UI now wraps button actions with a safe runner:
- Prevents repeated double clicks from stacking heavy operations.
- Catches exceptions instead of freezing silently.
- Shows the last UI action in the HUD help line.
- Reset now resumes time instead of staying paused, avoiding the impression that the app is frozen.

### 4. Heavy controls were reduced
- Max particle slider capped to 3000 from the UI.
- Bucket liquid quality sliders are capped lower for real-time interaction.
- Bucket geometry sliders request a rebuild instead of forcing full mesh rebuild continuously.
- Step Film runs a limited partial step instead of solving the whole film in one button click.

## Important runtime controls
In the UI, open **BUCKET INTERNAL PAINT**:
- Keep `X-Ray Bucket View` OFF for realistic view.
- Use `SAFE RECALIBRATE LIQUID` after changing the bucket model.
- Use `Liquid Center X/Z` only if the imported bucket mesh is visually offset.
- Use `Rim Level` and `Bottom Level` only for fine adjustment after auto-fit.

## Known limitation
This version was code-validated and structurally checked outside the Unity Editor. Final visual tuning still needs one Play Mode screenshot because exact imported FBX geometry is only fully evaluated inside Unity.
