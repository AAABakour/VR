using UnityEngine;

[DefaultExecutionOrder(-120)]
public class SimulationPerformanceOptimizer : MonoBehaviour
{
    public enum PerformanceMode
    {
        LowResource,
        FinalBalanced,
        PresentationQuality,
        SPHReady,
        UltraSPHPreparation
    }

    private const string RuntimeObjectName = "SimulationPerformanceOptimizer_Runtime";

    [Header("Startup")]
    public bool autoApplyOnStart = true;
    public PerformanceMode startupMode = PerformanceMode.UltraSPHPreparation;
    public int balancedTargetFrameRate = 90;
    public int presentationTargetFrameRate = 60;
    public int lowResourceTargetFrameRate = 60;
    public int ultraSphTargetFrameRate = 90;

    [Header("References")]
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;
    public PaintFilmFluidSolverV2 fluidFilmSolver;
    public BucketInteriorLiquidSystemV2 bucketLiquid;
    public RigRopeController ropeController;
    public PendulumController pendulumController;
    public ExperimentExporter exporter;
    public SphParticleBudgetController sphBudgetController;

    [Header("Runtime Readout")]
    [SerializeField] private PerformanceMode currentMode = PerformanceMode.UltraSPHPreparation;
    [SerializeField] private string lastSummary = "not applied yet";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SimulationPerformanceOptimizer>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SimulationPerformanceOptimizer>();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        AutoFindReferences();
        if (autoApplyOnStart)
        {
            ApplyProfile(startupMode);
        }
    }

    private void AutoFindReferences()
    {
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (particleSimulator == null) particleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (fluidFilmSolver == null) fluidFilmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        if (bucketLiquid == null) bucketLiquid = Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
        if (ropeController == null) ropeController = Object.FindFirstObjectByType<RigRopeController>();
        if (pendulumController == null) pendulumController = Object.FindFirstObjectByType<PendulumController>();
        if (exporter == null) exporter = Object.FindFirstObjectByType<ExperimentExporter>();
        if (sphBudgetController == null) sphBudgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
    }

    public void ApplyProfile(PerformanceMode mode)
    {
        AutoFindReferences();
        currentMode = mode;

        switch (mode)
        {
            case PerformanceMode.LowResource:
                ApplyLowResource();
                break;
            case PerformanceMode.PresentationQuality:
                ApplyPresentationQuality();
                break;
            case PerformanceMode.SPHReady:
                ApplySphReady();
                break;
            case PerformanceMode.UltraSPHPreparation:
                ApplyUltraSphPreparation();
                break;
            default:
                ApplyFinalBalanced();
                break;
        }

        lastSummary = BuildSummary();
        Debug.Log("[VR Performance] " + lastSummary);
    }

    private void ApplyLowResource()
    {
        ApplyEngineBudget(lowResourceTargetFrameRate, 0.02f, false);
        ApplyPaintEmitterBudget(0.22f, 55f, 80, 30f);
        ApplyParticleBudget(650, 70, 6, false, true);
        ApplyCanvasBudget(0.10f, false, 1.8f, 0.35f);
        ApplyFilmBudget(false, 0.085f, 1, 9000, 8.0f, 1.2f);
        ApplyBucketBudget(56, 12, 6, 6, 0.095f, false, false);
        ApplyRopeBudget(20, 42, 1, 0.99f, 0.025f);
        ApplyExportBudget(true, true);
    }

    private void ApplyFinalBalanced()
    {
        ApplyEngineBudget(balancedTargetFrameRate, 0.0166667f, false);
        ApplyPaintEmitterBudget(0.30f, 78f, 130, 60f);
        ApplyParticleBudget(1100, 110, 10, true, true);
        ApplyCanvasBudget(0.065f, true, 3.0f, 0.48f);
        ApplyFilmBudget(true, 0.055f, 2, 20000, 5.6f, 2.1f);
        ApplyBucketBudget(72, 18, 8, 12, 0.065f, false, true);
        ApplyRopeBudget(28, 70, 2, 0.98f, 0.045f);
        ApplyExportBudget(true, true);
    }

    private void ApplyPresentationQuality()
    {
        ApplyEngineBudget(presentationTargetFrameRate, 0.0166667f, true);
        ApplyPaintEmitterBudget(0.34f, 95f, 170, 90f);
        ApplyParticleBudget(1700, 150, 14, true, true);
        ApplyCanvasBudget(0.045f, true, 4.5f, 0.58f);
        ApplyFilmBudget(true, 0.040f, 2, 36000, 4.8f, 2.7f);
        ApplyBucketBudget(96, 24, 10, 20, 0.045f, false, true);
        ApplyRopeBudget(32, 90, 2, 0.98f, 0.055f);
        ApplyExportBudget(true, true);
    }

    private void ApplySphReady()
    {
        // This profile keeps this module visually strong but light enough to receive an external SPH solver later.
        ApplyEngineBudget(balancedTargetFrameRate, 0.0166667f, false);
        ApplyPaintEmitterBudget(0.18f, 42f, 60, 24f);
        ApplyParticleBudget(420, 50, 4, false, true);
        ApplyCanvasBudget(0.08f, true, 2.8f, 0.42f);
        ApplyFilmBudget(true, 0.070f, 1, 12000, 6.5f, 1.6f);
        ApplyBucketBudget(64, 14, 6, 8, 0.080f, false, true);
        ApplyRopeBudget(28, 64, 1, 0.985f, 0.040f);
        ApplyExportBudget(true, true);
    }

    private void ApplyUltraSphPreparation()
    {
        // Delivery profile for the next GPU SPH phase: the current CPU droplet system is reduced to a fallback,
        // while the bucket/canvas/film systems stay visually active and ready to receive batched SPH impacts.
        ApplyEngineBudget(ultraSphTargetFrameRate, 0.0166667f, false);
        ApplyPaintEmitterBudget(0.12f, 36f, 40, 18f);
        ApplyParticleBudget(384, 24, 0, false, true);
        ApplyCanvasBudget(0.080f, true, 3.2f, 0.46f);
        ApplyFilmBudget(true, 0.075f, 1, 12000, 6.8f, 1.5f);
        ApplyBucketBudget(64, 14, 6, 8, 0.085f, false, true);
        ApplyRopeBudget(28, 64, 1, 0.985f, 0.040f);
        ApplyExportBudget(true, true);

        if (sphBudgetController != null)
        {
            sphBudgetController.ApplyHighScalePreparation(false);
        }
    }

    private void ApplyEngineBudget(int targetFrameRate, float fixedDeltaTime, bool vSync)
    {
        Application.targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 240);
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        Time.fixedDeltaTime = Mathf.Clamp(fixedDeltaTime, 0.008f, 0.033f);
        Time.maximumDeltaTime = 0.08f;
    }

    private void ApplyPaintEmitterBudget(float flowRate, float particlesPerFlow, int burstLimit, float backlogLimit)
    {
        if (paintEmitter == null) return;
        paintEmitter.ApplyPerformanceBudget(flowRate, particlesPerFlow, burstLimit, backlogLimit);
    }

    private void ApplyParticleBudget(int particleBudget, int visualBudget, int interactionChecks, bool interactionsEnabled, bool visualDropletsEnabled)
    {
        if (particleSimulator == null) return;
        particleSimulator.ApplyPerformanceBudget(particleBudget, visualBudget, interactionChecks, interactionsEnabled, visualDropletsEnabled);
    }

    private void ApplyCanvasBudget(float textureInterval, bool normalMap, float normalStrength, float thicknessVisibility)
    {
        if (canvasPainter == null) return;
        canvasPainter.fluidTextureUpdateInterval = Mathf.Clamp(textureInterval, 0.02f, 0.25f);
        canvasPainter.generateFluidNormalMap = normalMap;
        canvasPainter.fluidNormalStrength = Mathf.Clamp(normalStrength, 0f, 8f);
        canvasPainter.fluidThicknessVisibility = Mathf.Clamp(thicknessVisibility, 0f, 1.8f);
    }

    private void ApplyFilmBudget(bool enabled, float interval, int substeps, int maxCells, float viscosity, float gravityStrength)
    {
        if (fluidFilmSolver == null) return;
        fluidFilmSolver.enableFluidFilm = enabled;
        fluidFilmSolver.solveWholeFilm = false;
        fluidFilmSolver.solverInterval = Mathf.Clamp(interval, 0.008f, 0.20f);
        fluidFilmSolver.subSteps = Mathf.Clamp(substeps, 1, 6);
        fluidFilmSolver.maxCellsPerFrame = Mathf.Clamp(maxCells, 1000, 65536);
        fluidFilmSolver.viscosity = Mathf.Clamp(viscosity, 0.2f, 16f);
        fluidFilmSolver.gravityStrength = Mathf.Clamp(gravityStrength, 0f, 10f);
    }

    private void ApplyBucketBudget(int segments, int rings, int sideSubdivisions, int rimHighlights, float rebuildInterval, bool volume, bool rimDetails)
    {
        if (bucketLiquid == null) return;
        bucketLiquid.presentationBucketTransparency = false;
        bucketLiquid.renderLiquidVolume = false;
        bucketLiquid.renderWetInnerWall = true;
        bucketLiquid.renderMeniscus = true;
        bucketLiquid.ApplyQualityBudget(segments, rings, sideSubdivisions, rimHighlights, rebuildInterval, volume, rimDetails);
    }

    private void ApplyRopeBudget(int segments, int iterations, int substeps, float tautness, float maxSag)
    {
        if (ropeController == null) return;
        ropeController.segmentCount = Mathf.Clamp(segments, 8, 96);
        ropeController.constraintIterations = Mathf.Clamp(iterations, 4, 160);
        ropeController.solverSubsteps = Mathf.Clamp(substeps, 1, 8);
        ropeController.tautness = Mathf.Clamp01(tautness);
        ropeController.maxSag = Mathf.Max(0f, maxSag);
        ropeController.ResetRopeNow();
    }

    private void ApplyExportBudget(bool image, bool report)
    {
        if (exporter == null) return;
        exporter.allowImageExport = image;
        exporter.allowReportExport = report;
    }

    private string BuildSummary()
    {
        string particles = particleSimulator != null ? particleSimulator.maxParticles.ToString() : "--";
        string film = fluidFilmSolver != null ? (fluidFilmSolver.solverInterval.ToString("0.000") + "s / " + fluidFilmSolver.maxCellsPerFrame + " cells") : "--";
        string bucket = bucketLiquid != null ? (bucketLiquid.radialSegments + "x" + bucketLiquid.radialRings + " @ " + bucketLiquid.meshRebuildInterval.ToString("0.000") + "s") : "--";
        string rope = ropeController != null ? (ropeController.segmentCount + " seg / " + ropeController.constraintIterations + " it") : "--";
        string sph = sphBudgetController != null ? (" | " + sphBudgetController.targetParticleCount.ToString("N0") + " SPH target / stride " + sphBudgetController.renderStride) : "";
        return currentMode + " | particles " + particles + " | film " + film + " | bucket " + bucket + " | rope " + rope + sph;
    }

    public PerformanceMode CurrentMode => currentMode;
    public string LastSummary => lastSummary;
}
