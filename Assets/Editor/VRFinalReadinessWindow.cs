#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class VRFinalReadinessWindow
{
    [MenuItem("Tools/VR Paint/Run Final Readiness Scan")]
    public static void RunFinalReadinessScan()
    {
        StringBuilder report = new StringBuilder(4096);
        report.AppendLine("Swinging Paint Bucket Simulation - Editor Readiness Scan");
        report.AppendLine("Generated: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        AddLine(report, "Unity version", Application.unityVersion);
        AddLine(report, "Active scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        AddLine(report, "Target FPS", Application.targetFrameRate.ToString());
        AddLine(report, "Fixed Delta", Time.fixedDeltaTime.ToString("0.0000"));
        AddLine(report, "VSync", QualitySettings.vSyncCount.ToString());
        report.AppendLine();

        AddObjectCheck<SimulationPerformanceOptimizer>(report, "Performance optimizer");
        AddObjectCheck<RuntimeAdaptiveQualityGovernor>(report, "Adaptive quality governor");
        AddObjectCheck<SphParticleBudgetController>(report, "SPH budget controller");
        AddObjectCheck<SphCollisionSurfaceBridge>(report, "SPH collision bridge");
        AddObjectCheck<PaintEmitter>(report, "Paint emitter");
        AddObjectCheck<PaintParticleSimulator>(report, "Legacy paint particles");
        AddObjectCheck<CanvasPainter>(report, "Canvas painter");
        AddObjectCheck<PaintImpactEngineV2>(report, "Impact engine V2");
        AddObjectCheck<PaintSurfaceStateV2>(report, "Surface state V2");
        AddObjectCheck<PaintFilmFluidSolverV2>(report, "Surface film solver");
        AddObjectCheck<BucketInteriorLiquidSystemV2>(report, "Bucket interior liquid");
        AddObjectCheck<RigRopeController>(report, "Rope controller");
        AddObjectCheck<ModernSimulationGameUI>(report, "Modern game UI");
        report.AppendLine();

        SphParticleBudgetController sph = Object.FindFirstObjectByType<SphParticleBudgetController>();
        if (sph != null)
        {
            report.AppendLine("SPH readiness: " + sph.BuildReadinessSummary());
        }
        else
        {
            report.AppendLine("SPH readiness: controller will auto-create at runtime if no scene object exists.");
        }

        string folder = "Assets/Reports";
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "VR_EditorReadinessScan.txt");
        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("[VR Editor Readiness] Report written to " + path + "\n" + report);
    }

    private static void AddObjectCheck<T>(StringBuilder report, string label) where T : Object
    {
        T obj = Object.FindFirstObjectByType<T>();
        report.AppendLine((obj != null ? "[PASS] " : "[INFO] ") + label + ": " + (obj != null ? obj.name : "not in open scene / runtime auto-create may handle it"));
    }

    private static void AddLine(StringBuilder report, string label, string value)
    {
        report.AppendLine(label + ": " + value);
    }
}
#endif
