#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RealGpuSphPhase04Tools
{
    [MenuItem("Tools/VR Paint/SPH Phase 04/Add Runtime Controller And Visual Volume")]
    public static void AddRuntimeControllerToScene()
    {
        RealGpuSphController controller = Object.FindFirstObjectByType<RealGpuSphController>();
        if (controller == null)
        {
            GameObject obj = new GameObject("RealGpuSphController_Phase04");
            controller = obj.AddComponent<RealGpuSphController>();
            Undo.RegisterCreatedObjectUndo(obj, "Add Real GPU SPH Controller");
        }

        controller.solverCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Resources/RealBucketSPH_Phase02.compute");
        Shader particleShader = Shader.Find("VR/Paint/GPU SPH Indirect URP");
        if (controller.renderMaterial == null && particleShader != null)
        {
            controller.renderMaterial = new Material(particleShader);
            controller.renderMaterial.name = "Editor_GpuSphIndirectPaint_Phase04";
        }

        GameObject bucket = GameObject.Find("Bucket");
        if (bucket != null)
        {
            controller.bucketTransform = bucket.transform;
            controller.bucketRenderer = bucket.GetComponentInChildren<Renderer>();
        }

        CanvasPainter painter = Object.FindFirstObjectByType<CanvasPainter>();
        if (painter != null)
        {
            controller.surfacePlane = painter.transform;
        }

        SphFluidVolumeVisualRenderer visual = Object.FindFirstObjectByType<SphFluidVolumeVisualRenderer>();
        if (visual == null)
        {
            GameObject visualObject = new GameObject("SphFluidVolumeVisualRenderer_Phase04");
            visual = visualObject.AddComponent<SphFluidVolumeVisualRenderer>();
            Undo.RegisterCreatedObjectUndo(visualObject, "Add Phase 04 SPH Visual Volume");
        }

        visual.gpuSphController = controller;
        if (controller.bucketTransform != null) visual.bucketTransform = controller.bucketTransform;
        if (controller.bucketRenderer != null) visual.bucketRenderer = controller.bucketRenderer;
        visual.quality = SphFluidVolumeVisualRenderer.VisualQuality.High;
        visual.enableVisualVolume = true;

        controller.volumeVisualRenderer = visual;
        controller.budgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
        controller.enableRuntime = false;
        controller.particleCapacity = 262144;
        controller.renderStride = 16;
        controller.enablePhase04VisualReconstruction = true;
        controller.softenContainedParticles = true;

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(visual);
        Selection.activeObject = controller.gameObject;
        Debug.Log("[VR SPH Phase04] Runtime controller and visual volume are ready. Press Play, then F9 for preview or F11 for million-particle mode.");
    }

    [MenuItem("Tools/VR Paint/SPH Phase 04/Run Phase 04 Validation")]
    public static void RunPhase04Validation()
    {
        int passes = 0;
        int warnings = 0;
        int failures = 0;

        ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Resources/RealBucketSPH_Phase02.compute");
        Shader particleShader = Shader.Find("VR/Paint/GPU SPH Indirect URP");
        Shader volumeShader = Shader.Find("VR/Paint/SPH Volume Surface URP");
        RealGpuSphController controller = Object.FindFirstObjectByType<RealGpuSphController>();
        SphFluidVolumeVisualRenderer visual = Object.FindFirstObjectByType<SphFluidVolumeVisualRenderer>();
        SphParticleBudgetController budget = Object.FindFirstObjectByType<SphParticleBudgetController>();
        CanvasPainter painter = Object.FindFirstObjectByType<CanvasPainter>();

        Check(compute != null, "RealBucketSPH compute is available", ref passes, ref warnings, ref failures, true);
        if (compute != null)
        {
            Check(HasKernel(compute, "CSClearGrid"), "GPU grid clear kernel exists", ref passes, ref warnings, ref failures, true);
            Check(HasKernel(compute, "CSBuildGrid"), "GPU grid build kernel exists", ref passes, ref warnings, ref failures, true);
            Check(HasKernel(compute, "CSComputeDensityPressure"), "Density/pressure kernel exists", ref passes, ref warnings, ref failures, true);
            Check(HasKernel(compute, "CSIntegrateSph"), "SPH integration kernel exists", ref passes, ref warnings, ref failures, true);
            Check(HasKernel(compute, "CSGenerateRenderParticles"), "Phase04 render generation kernel exists", ref passes, ref warnings, ref failures, true);
        }

        Check(particleShader != null, "GPU SPH indirect URP particle shader is discoverable", ref passes, ref warnings, ref failures, true);
        Check(volumeShader != null, "Phase04 smooth volume surface shader is discoverable", ref passes, ref warnings, ref failures, true);
        Check(controller != null, "RealGpuSphController exists in scene or will auto-create at runtime", ref passes, ref warnings, ref failures, false);
        Check(visual != null, "SphFluidVolumeVisualRenderer exists in scene or will auto-create at runtime", ref passes, ref warnings, ref failures, false);
        Check(budget != null && budget.targetParticleCount >= 1000000, "SPH budget target supports 1,000,000+ particles", ref passes, ref warnings, ref failures, true);
        Check(painter != null, "CanvasPainter exists for surface collision preview alignment", ref passes, ref warnings, ref failures, false);
        Check(SystemInfo.supportsComputeShaders, "Current editor machine reports compute shader support", ref passes, ref warnings, ref failures, true);
        Check(SystemInfo.graphicsMemorySize >= 2048, "GPU memory appears adequate for the 1M phase: " + SystemInfo.graphicsMemorySize + " MB", ref passes, ref warnings, ref failures, false);

        string summary = "[VR SPH Phase04] Validation " + (failures == 0 ? "PASS" : "FAIL") + ": " + passes + " passed, " + warnings + " warnings, " + failures + " failed.";
        Debug.Log(summary);
        EditorUtility.DisplayDialog("VR SPH Phase 04 Validation", summary, "OK");
    }

    private static bool HasKernel(ComputeShader compute, string kernelName)
    {
        try
        {
            compute.FindKernel(kernelName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Check(bool condition, string message, ref int passes, ref int warnings, ref int failures, bool failIfFalse)
    {
        if (condition)
        {
            passes++;
            Debug.Log("[VR SPH Phase04][PASS] " + message);
        }
        else if (failIfFalse)
        {
            failures++;
            Debug.LogError("[VR SPH Phase04][FAIL] " + message);
        }
        else
        {
            warnings++;
            Debug.LogWarning("[VR SPH Phase04][WARN] " + message);
        }
    }
}
#endif
