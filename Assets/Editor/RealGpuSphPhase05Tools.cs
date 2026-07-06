#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RealGpuSphPhase05Tools
{
    [MenuItem("Tools/VR Paint/SPH Phase 05/Add Collision And Impact Quality Stack")]
    public static void AddPhase05Stack()
    {
        RealGpuSphController controller = Object.FindFirstObjectByType<RealGpuSphController>();
        if (controller == null)
        {
            GameObject controllerObject = new GameObject("RealGpuSphController_Phase05");
            controller = controllerObject.AddComponent<RealGpuSphController>();
            Undo.RegisterCreatedObjectUndo(controllerObject, "Add Real GPU SPH Controller");
        }

        controller.solverCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Resources/RealBucketSPH_Phase02.compute");
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

        SphCollisionSurfaceBridge bridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        if (bridge == null)
        {
            GameObject bridgeObject = new GameObject("SphCollisionSurfaceBridge_Phase05");
            bridge = bridgeObject.AddComponent<SphCollisionSurfaceBridge>();
            Undo.RegisterCreatedObjectUndo(bridgeObject, "Add SPH Collision Bridge");
        }

        SphPhase05ImpactDirector director = Object.FindFirstObjectByType<SphPhase05ImpactDirector>();
        if (director == null)
        {
            GameObject directorObject = new GameObject("SphPhase05ImpactDirector");
            director = directorObject.AddComponent<SphPhase05ImpactDirector>();
            Undo.RegisterCreatedObjectUndo(directorObject, "Add SPH Phase05 Impact Director");
        }

        Phase05WetPaintImpactQualityTuner tuner = Object.FindFirstObjectByType<Phase05WetPaintImpactQualityTuner>();
        if (tuner == null)
        {
            GameObject tunerObject = new GameObject("Phase05WetPaintImpactQualityTuner");
            tuner = tunerObject.AddComponent<Phase05WetPaintImpactQualityTuner>();
            Undo.RegisterCreatedObjectUndo(tunerObject, "Add Phase05 Wet Paint Tuner");
        }

        director.gpuSphController = controller;
        director.collisionBridge = bridge;
        if (bucket != null) director.bucketTransform = bucket.transform;
        if (painter != null)
        {
            director.canvasPainter = painter;
            director.surfacePlane = painter.transform;
        }

        tuner.canvasPainter = painter;
        tuner.impactDirector = director;
        tuner.collisionBridge = bridge;
        tuner.splatGenerator = Object.FindFirstObjectByType<AdvancedSplatGeneratorV2>();
        tuner.impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        tuner.surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        tuner.filmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        tuner.dripSolver = Object.FindFirstObjectByType<PaintDripSolverV2>();
        tuner.ApplyQualityTuning();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(tuner);

        Selection.activeObject = director.gameObject;
        Debug.Log("[VR SPH Phase05] Collision and impact quality stack is installed. Press Play, F9 to run SPH, then tilt/swing the bucket to test cohesive impacts.");
    }

    [MenuItem("Tools/VR Paint/SPH Phase 05/Run Phase 05 Validation")]
    public static void RunPhase05Validation()
    {
        int passes = 0;
        int warnings = 0;
        int failures = 0;

        RealGpuSphController controller = Object.FindFirstObjectByType<RealGpuSphController>();
        SphCollisionSurfaceBridge bridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        SphPhase05ImpactDirector director = Object.FindFirstObjectByType<SphPhase05ImpactDirector>();
        Phase05WetPaintImpactQualityTuner tuner = Object.FindFirstObjectByType<Phase05WetPaintImpactQualityTuner>();
        PaintImpactEngineV2 impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        AdvancedSplatGeneratorV2 splat = Object.FindFirstObjectByType<AdvancedSplatGeneratorV2>();
        CanvasPainter painter = Object.FindFirstObjectByType<CanvasPainter>();
        PaintSurfaceStateV2 surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        PaintFilmFluidSolverV2 filmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();

        Check(controller != null, "Real GPU SPH controller exists or runtime auto-create is available", ref passes, ref warnings, ref failures, false);
        Check(bridge != null, "SPH collision bridge exists or runtime auto-create is available", ref passes, ref warnings, ref failures, false);
        Check(director != null, "Phase05 impact director exists or runtime auto-create is available", ref passes, ref warnings, ref failures, false);
        Check(tuner != null, "Phase05 wet paint quality tuner exists or runtime auto-create is available", ref passes, ref warnings, ref failures, false);
        Check(impactEngine != null, "PaintImpactEngineV2 exists", ref passes, ref warnings, ref failures, true);
        Check(splat != null, "AdvancedSplatGeneratorV2 exists", ref passes, ref warnings, ref failures, true);
        Check(painter != null, "CanvasPainter exists", ref passes, ref warnings, ref failures, true);
        Check(surfaceState != null, "PaintSurfaceStateV2 exists", ref passes, ref warnings, ref failures, true);
        Check(filmSolver != null, "PaintFilmFluidSolverV2 exists", ref passes, ref warnings, ref failures, true);

        if (bridge != null)
        {
            Check(bridge.enableSpatialCoalescing, "Spatial coalescing enabled", ref passes, ref warnings, ref failures, false);
            Check(bridge.maxClusteredImpactsPerFrame <= 1024, "Cluster budget is delivery-safe", ref passes, ref warnings, ref failures, false);
        }

        if (director != null)
        {
            Check(director.maxMacroImpactsPerFrame <= 6, "Macro impact budget is delivery-safe", ref passes, ref warnings, ref failures, false);
            Check(director.baseImpactRadius >= 0.035f, "Cohesive impact radius is visually meaningful", ref passes, ref warnings, ref failures, false);
        }

        string summary = "[VR SPH Phase05] Validation " + (failures == 0 ? "PASS" : "FAIL") + ": " + passes + " passed, " + warnings + " warnings, " + failures + " failed.";
        Debug.Log(summary);
        EditorUtility.DisplayDialog("VR SPH Phase 05 Validation", summary, "OK");
    }

    private static void Check(bool condition, string message, ref int passes, ref int warnings, ref int failures, bool failIfFalse)
    {
        if (condition)
        {
            passes++;
            Debug.Log("[VR SPH Phase05][PASS] " + message);
        }
        else if (failIfFalse)
        {
            failures++;
            Debug.LogError("[VR SPH Phase05][FAIL] " + message);
        }
        else
        {
            warnings++;
            Debug.LogWarning("[VR SPH Phase05][WARN] " + message);
        }
    }
}
#endif
