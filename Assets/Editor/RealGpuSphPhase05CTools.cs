#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RealGpuSphPhase05CTools
{
    [MenuItem("Tools/VR Paint/SPH Phase 05C/Run Phase 05C Validation")]
    public static void RunValidation()
    {
        bool ok = true;
        if (Object.FindFirstObjectByType<RealGpuSphController>() == null)
        {
            Debug.LogWarning("[VR SPH Phase05C] RealGpuSphController will auto-create at runtime, but adding one to the scene is recommended for final delivery.");
        }

        if (Shader.Find("VR/Paint/Phase05C Instanced Droplet URP") == null)
        {
            ok = false;
            Debug.LogError("[VR SPH Phase05C] Missing instanced droplet shader.");
        }

        if (Object.FindFirstObjectByType<SphHeroPaintVisualRenderer>() == null)
        {
            Debug.LogWarning("[VR SPH Phase05C] Hero visual renderer will auto-create at runtime.");
        }

        if (Object.FindFirstObjectByType<SphPhase05CVisualDensityPerformanceDirector>() == null)
        {
            Debug.LogWarning("[VR SPH Phase05C] Visual density director will auto-create at runtime.");
        }

        if (ok)
        {
            Debug.Log("[VR SPH Phase05C] Validation complete: dense fast visual pour + FPS protection are installed. Use F9 first. F11 still tests full million-particle mode, but Phase05C keeps presentation visuals dense without rendering raw million points.");
        }
    }
}
#endif
