#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RealGpuSphPhase05BTools
{
    [MenuItem("Tools/VR Paint/SPH Phase 05B/Run Phase 05B Validation")]
    public static void RunValidation()
    {
        bool ok = true;

        RealGpuSphController controller = Object.FindFirstObjectByType<RealGpuSphController>();
        SphHeroPaintVisualRenderer hero = Object.FindFirstObjectByType<SphHeroPaintVisualRenderer>();
        SphPhase05ImpactDirector impacts = Object.FindFirstObjectByType<SphPhase05ImpactDirector>();
        SphCollisionSurfaceBridge bridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        PaintSurfaceStateV2 surface = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        CanvasPainter canvas = Object.FindFirstObjectByType<CanvasPainter>();

        if (controller == null) { Debug.LogWarning("[Phase05B Validation] RealGpuSphController is missing. It will auto-create in Play Mode."); }
        if (hero == null) { Debug.LogWarning("[Phase05B Validation] SphHeroPaintVisualRenderer is missing. It will auto-create in Play Mode."); }
        if (impacts == null) { Debug.LogWarning("[Phase05B Validation] SphPhase05ImpactDirector is missing. It will auto-create in Play Mode."); }
        if (bridge == null) { Debug.LogWarning("[Phase05B Validation] SphCollisionSurfaceBridge is missing. It will auto-create in Play Mode."); }
        if (surface == null) { Debug.LogWarning("[Phase05B Validation] PaintSurfaceStateV2 not found. Thickness map quality may be limited."); ok = false; }
        if (canvas == null) { Debug.LogWarning("[Phase05B Validation] CanvasPainter not found. Visual surface composite may be limited."); ok = false; }

        Shader thickShader = Shader.Find("VR/Paint/Thick Lens Paint URP");
        if (thickShader == null)
        {
            Debug.LogWarning("[Phase05B Validation] Thick Lens shader was not found yet. Unity may still be importing assets; reopen validation after import.");
            ok = false;
        }

        if (ok)
        {
            Debug.Log("[Phase05B Validation] PASS: Cohesive pour + raised wet thickness layer is installed. Press Play then F9. Start from Phase05, not Phase06.");
        }
        else
        {
            Debug.LogWarning("[Phase05B Validation] Finished with warnings. Check the lines above.");
        }
    }
}
#endif
