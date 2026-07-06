#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VRFinalQaEditorValidator
{
    private const string MainScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/VR Final QA/Run Static Scene Validation")]
    public static void RunStaticSceneValidation()
    {
        StringBuilder report = new StringBuilder(4096);
        int pass = 0;
        int warn = 0;
        int fail = 0;

        report.AppendLine("Swinging Paint Bucket Simulation - Editor Static Validation");
        report.AppendLine("Scene: " + MainScenePath);
        report.AppendLine();

        EditorSceneManager.OpenScene(MainScenePath);

        CheckObject("Bucket", ref pass, ref fail, report);
        CheckObject("CanvasBoard", ref pass, ref fail, report);
        CheckObject("SimulationManager", ref pass, ref fail, report);
        CheckObject("RigRopeSystem", ref pass, ref fail, report);

        CheckComponent<SimulationManager>("SimulationManager", ref pass, ref fail, report);
        CheckComponent<PaintEmitter>("PaintEmitter", ref pass, ref fail, report);
        CheckComponent<PaintParticleSimulator>("PaintParticleSimulator", ref pass, ref fail, report);
        CheckComponent<CanvasPainter>("CanvasPainter", ref pass, ref fail, report);
        CheckComponent<PaintImpactEngineV2>("PaintImpactEngineV2", ref pass, ref fail, report);
        CheckComponent<PaintSurfaceStateV2>("PaintSurfaceStateV2", ref pass, ref fail, report);
        CheckComponent<PaintFilmFluidSolverV2>("PaintFilmFluidSolverV2", ref pass, ref fail, report);
        CheckComponent<BucketInteriorLiquidSystemV2>("BucketInteriorLiquidSystemV2", ref pass, ref fail, report);
        CheckComponent<RigRopeController>("RigRopeController", ref pass, ref fail, report);
        CheckComponent<PendulumController>("PendulumController", ref pass, ref fail, report);

        PaintEmitter emitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (emitter != null)
        {
            Check(emitter.clampParticleBurstPerFrame, "Emitter burst clamp enabled", ref pass, ref warn, report);
            Check(emitter.maxParticlesEmittedPerFrame <= 240, "Emitter per-frame burst <= 240", ref pass, ref warn, report);
        }

        PaintParticleSimulator particles = Object.FindFirstObjectByType<PaintParticleSimulator>();
        if (particles != null)
        {
            Check(particles.maxParticles <= 3000, "Pre-SPH particle budget <= 3000", ref pass, ref warn, report);
            Check(particles.maxInteractionChecks <= 16, "Interaction checks <= 16", ref pass, ref warn, report);
        }

        PaintFilmFluidSolverV2 film = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        if (film != null)
        {
            Check(!film.solveWholeFilm, "Fluid film uses bounded solving", ref pass, ref warn, report);
            Check(film.subSteps <= 3, "Fluid substeps <= 3", ref pass, ref warn, report);
        }

        BucketInteriorLiquidSystemV2 bucket = Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
        if (bucket != null)
        {
            Check(!bucket.presentationBucketTransparency, "Bucket opaque by default", ref pass, ref warn, report);
            Check(bucket.meshRebuildInterval >= 0.04f, "Bucket liquid rebuild interval >= 0.04", ref pass, ref warn, report);
        }

        string summary = "PASS " + pass + " | WARN " + warn + " | FAIL " + fail;
        report.AppendLine();
        report.AppendLine(summary);

        string folder = "Assets/Reports";
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "VR_EDITOR_STATIC_VALIDATION_LAST_RUN.txt");
        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("[VR Final QA] " + summary + "\n" + report);
    }

    private static void CheckObject(string objectName, ref int pass, ref int fail, StringBuilder report)
    {
        bool ok = GameObject.Find(objectName) != null;
        if (ok) pass++; else fail++;
        report.AppendLine((ok ? "[PASS] " : "[FAIL] ") + "Object exists: " + objectName);
    }

    private static void CheckComponent<T>(string label, ref int pass, ref int fail, StringBuilder report) where T : Object
    {
        bool ok = Object.FindFirstObjectByType<T>() != null;
        if (ok) pass++; else fail++;
        report.AppendLine((ok ? "[PASS] " : "[FAIL] ") + "Component exists: " + label);
    }

    private static void Check(bool condition, string label, ref int pass, ref int warn, StringBuilder report)
    {
        if (condition) pass++; else warn++;
        report.AppendLine((condition ? "[PASS] " : "[WARN] ") + label);
    }
}
#endif
