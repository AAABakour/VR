using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1200)]
public class SimulationSelfTestSuite : MonoBehaviour
{
    private const string RuntimeObjectName = "SimulationSelfTestSuite_Runtime";

    [Header("Startup QA")]
    public bool autoRunLightCheckOnStart = true;
    public float startupDelay = 0.75f;

    [Header("References")]
    public ModernSimulationGameUI modernUi;
    public SimulationManager simulationManager;
    public SimulationPerformanceOptimizer performanceOptimizer;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;
    public PaintImpactEngineV2 impactEngine;
    public PaintSurfaceStateV2 surfaceState;
    public PaintFilmFluidSolverV2 fluidFilmSolver;
    public BucketInteriorLiquidSystemV2 bucketLiquid;
    public RigRopeController ropeController;
    public PendulumController pendulumController;
    public ExperimentExporter exporter;

    [SerializeField] private string lastSummary = "not run";
    [SerializeField] private int passCount;
    [SerializeField] private int warningCount;
    [SerializeField] private int failCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (UnityEngine.Object.FindFirstObjectByType<SimulationSelfTestSuite>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SimulationSelfTestSuite>();
    }

    private IEnumerator Start()
    {
        AutoFindReferences();
        if (autoRunLightCheckOnStart)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, startupDelay));
            RunFullSelfTest(false);
        }
    }

    private void AutoFindReferences()
    {
        if (modernUi == null) modernUi = UnityEngine.Object.FindFirstObjectByType<ModernSimulationGameUI>();
        if (simulationManager == null) simulationManager = UnityEngine.Object.FindFirstObjectByType<SimulationManager>();
        if (performanceOptimizer == null) performanceOptimizer = UnityEngine.Object.FindFirstObjectByType<SimulationPerformanceOptimizer>();
        if (paintEmitter == null) paintEmitter = UnityEngine.Object.FindFirstObjectByType<PaintEmitter>();
        if (particleSimulator == null) particleSimulator = UnityEngine.Object.FindFirstObjectByType<PaintParticleSimulator>();
        if (canvasPainter == null) canvasPainter = UnityEngine.Object.FindFirstObjectByType<CanvasPainter>();
        if (impactEngine == null) impactEngine = UnityEngine.Object.FindFirstObjectByType<PaintImpactEngineV2>();
        if (surfaceState == null) surfaceState = UnityEngine.Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        if (fluidFilmSolver == null) fluidFilmSolver = UnityEngine.Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        if (bucketLiquid == null) bucketLiquid = UnityEngine.Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
        if (ropeController == null) ropeController = UnityEngine.Object.FindFirstObjectByType<RigRopeController>();
        if (pendulumController == null) pendulumController = UnityEngine.Object.FindFirstObjectByType<PendulumController>();
        if (exporter == null) exporter = UnityEngine.Object.FindFirstObjectByType<ExperimentExporter>();
    }

    public string RunFullSelfTestAndWriteReport()
    {
        string report = RunFullSelfTest(true);
        return lastSummary;
    }

    public string RunFullSelfTest(bool writeReport)
    {
        AutoFindReferences();
        passCount = 0;
        warningCount = 0;
        failCount = 0;

        StringBuilder report = new StringBuilder(4096);
        report.AppendLine("Swinging Paint Bucket Simulation - Final Runtime QA");
        report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        AddRequired(report, "SimulationManager", simulationManager);
        AddRequired(report, "ModernSimulationGameUI", modernUi);
        AddRequired(report, "SimulationPerformanceOptimizer", performanceOptimizer);
        AddRequired(report, "PaintEmitter", paintEmitter);
        AddRequired(report, "PaintParticleSimulator", particleSimulator);
        AddRequired(report, "CanvasPainter", canvasPainter);
        AddRequired(report, "PaintImpactEngineV2", impactEngine);
        AddRequired(report, "PaintSurfaceStateV2", surfaceState);
        AddRequired(report, "PaintFilmFluidSolverV2", fluidFilmSolver);
        AddRequired(report, "BucketInteriorLiquidSystemV2", bucketLiquid);
        AddRequired(report, "RigRopeController", ropeController);
        AddRequired(report, "PendulumController", pendulumController);
        AddRequired(report, "ExperimentExporter", exporter);

        report.AppendLine();
        report.AppendLine("UI checks");
        report.AppendLine("---------");
        if (modernUi != null)
        {
            AddCheck(report, modernUi.RegisteredButtonCount >= 20, "UI registered button count = " + modernUi.RegisteredButtonCount, false);
            AddCheck(report, modernUi.RegisteredSliderCount >= 20, "UI registered slider count = " + modernUi.RegisteredSliderCount, false);
            AddCheck(report, modernUi.RegisteredToggleCount >= 8, "UI registered toggle count = " + modernUi.RegisteredToggleCount, false);
            AddCheck(report, modernUi.RegisteredDropdownCount >= 2, "UI registered dropdown count = " + modernUi.RegisteredDropdownCount, false);
            AddMessage(report, modernUi.RunUiNonDestructiveSmokeTest());
        }

        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        int interactableButtons = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].interactable) interactableButtons++;
        }
        AddCheck(report, interactableButtons >= 20, "Interactable Unity UI buttons = " + interactableButtons, false);

        report.AppendLine();
        report.AppendLine("Performance checks");
        report.AppendLine("------------------");
        if (particleSimulator != null)
        {
            AddCheck(report, particleSimulator.maxParticles <= 3000, "Particle budget <= 3000 for pre-SPH module: " + particleSimulator.maxParticles, false);
            AddCheck(report, particleSimulator.maxInteractionChecks <= 16, "Particle interaction checks bounded: " + particleSimulator.maxInteractionChecks, false);
        }
        if (paintEmitter != null)
        {
            AddCheck(report, paintEmitter.clampParticleBurstPerFrame, "Emitter burst clamp enabled", false);
            AddCheck(report, paintEmitter.maxParticlesEmittedPerFrame <= 240, "Emitter per-frame burst bounded: " + paintEmitter.maxParticlesEmittedPerFrame, false);
        }
        if (fluidFilmSolver != null)
        {
            AddCheck(report, !fluidFilmSolver.solveWholeFilm || fluidFilmSolver.maxCellsPerFrame <= 65536, "Fluid solver bounded mode / safe full mode", false);
            AddCheck(report, fluidFilmSolver.subSteps <= 3, "Fluid substeps delivery-safe: " + fluidFilmSolver.subSteps, false);
        }
        if (bucketLiquid != null)
        {
            AddCheck(report, !bucketLiquid.presentationBucketTransparency, "Bucket is opaque by default", false);
            AddCheck(report, bucketLiquid.meshRebuildInterval >= 0.04f, "Interior liquid rebuild interval safe: " + bucketLiquid.meshRebuildInterval.ToString("0.000"), false);
            AddCheck(report, bucketLiquid.radialSegments <= 120, "Interior liquid mesh budget safe: " + bucketLiquid.radialSegments + " segments", false);
        }
        if (canvasPainter != null)
        {
            AddCheck(report, canvasPainter.fluidTextureUpdateInterval >= 0.04f, "Canvas fluid texture interval safe: " + canvasPainter.fluidTextureUpdateInterval.ToString("0.000"), false);
        }

        report.AppendLine();
        report.AppendLine("Integration readiness");
        report.AppendLine("---------------------");
        AddCheck(report, performanceOptimizer != null, "Performance optimizer available for SPH-ready profile", true);
        AddCheck(report, surfaceState != null && fluidFilmSolver != null && canvasPainter != null, "Surface film pipeline has swappable integration points", true);
        AddCheck(report, paintEmitter != null && particleSimulator != null, "Legacy emitter/particle layer can be throttled before SPH merge", true);

        lastSummary = "QA " + (failCount == 0 ? "PASS" : "FAIL") + ": " + passCount + " passed, " + warningCount + " warnings, " + failCount + " failed";
        report.AppendLine();
        report.AppendLine(lastSummary);

        if (writeReport)
        {
            WriteRuntimeReport(report.ToString());
        }

        Debug.Log("[VR QA] " + lastSummary + "\n" + report);
        return report.ToString();
    }

    private void AddRequired(StringBuilder report, string name, UnityEngine.Object reference)
    {
        AddCheck(report, reference != null, name + " reference", true);
    }

    private void AddCheck(StringBuilder report, bool condition, string message, bool failIfFalse)
    {
        if (condition)
        {
            passCount++;
            report.AppendLine("[PASS] " + message);
        }
        else if (failIfFalse)
        {
            failCount++;
            report.AppendLine("[FAIL] " + message);
        }
        else
        {
            warningCount++;
            report.AppendLine("[WARN] " + message);
        }
    }

    private void AddMessage(StringBuilder report, string message)
    {
        report.AppendLine("[INFO] " + message);
    }

    private void WriteRuntimeReport(string report)
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "SwingingPaintBucketExports");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "VR_FinalRuntimeQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(path, report, Encoding.UTF8);
            Debug.Log("[VR QA] Runtime QA report written to: " + path);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public string LastSummary => lastSummary;
}
