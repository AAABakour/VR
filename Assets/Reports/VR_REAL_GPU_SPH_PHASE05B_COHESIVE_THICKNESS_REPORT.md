# VR Real GPU SPH — Phase 05B Cohesive Pour + Raised Wet Thickness

This phase intentionally starts from **Phase 05 Collision Impact Quality** and does not use the rejected Phase 06 branch.

## Goal

Improve the stable Phase 05 result without introducing a noisy or fake-looking synthetic system:

- keep raw GPU particles hidden as the primary visual layer;
- make the falling paint read as a cohesive, heavy paint stream;
- reduce random spray and red particle dust;
- generate fewer but larger heavy paint impacts;
- add a raised glossy wet thickness layer above the canvas surface.

## Added Files

- `Assets/Scripts/SPH/SphPhase05BPaintQualityDirector.cs`
- `Assets/SPH/Shaders/SphThickLensPaint_URP.shader`
- `Assets/Editor/RealGpuSphPhase05BTools.cs`

## Runtime Behavior

`SphPhase05BPaintQualityDirector` is auto-created after scene load. It finds the existing SPH, impact, canvas, and bucket systems, then applies a non-destructive quality lock:

- disables raw GPU particle drawing;
- thickens the hero pour stream;
- reduces legacy particle emission noise;
- lowers satellite speckles;
- increases surface thickness and wetness response;
- creates a pool of raised wet paint lens meshes on the canvas during pour.

## Test Flow

1. Open the project in Unity.
2. Run `Tools > VR Paint > SPH Phase 05B > Run Phase 05B Validation`.
3. Enter Play Mode.
4. Press `F9`.
5. Tilt/swing the bucket.
6. Inspect the stream and the raised wet paint deposits.

Do not press F11 until the look is approved in normal mode.
