using UnityEngine;

[DefaultExecutionOrder(-115)]
public class SphParticleBudgetController : MonoBehaviour
{
    private const string RuntimeObjectName = "SphParticleBudgetController_Runtime";

    [Header("Mode")]
    public bool autoCreateOnSceneLoad = true;
    public bool applyHighScalePreparationOnStart = true;
    public bool logReadinessSummary = true;

    [Header("Optional Settings Asset")]
    public SphSimulationSettings settings;

    [Header("High Scale Defaults")]
    [Min(1024)] public int targetParticleCount = 1000000;
    [Range(1, 256)] public int renderStride = 12;
    [Range(128, 131072)] public int collisionBatchSize = 8192;
    [Range(64, 3000)] public int legacyCpuFallbackParticles = 384;
    [Range(0, 512)] public int legacyVisualDroplets = 24;

    [Header("Integration References")]
    public SimulationPerformanceOptimizer performanceOptimizer;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator legacyParticleSimulator;
    public CanvasPainter canvasPainter;
    public PaintFilmFluidSolverV2 fluidFilmSolver;
    public BucketInteriorLiquidSystemV2 bucketLiquid;
    public SphCollisionSurfaceBridge surfaceBridge;

    [Header("Runtime Readout")]
    [SerializeField] private bool highScalePreparationApplied;
    [SerializeField] private string lastReadinessSummary = "not prepared";
    [SerializeField] private int appliedVisibleParticleBudget;
    [SerializeField] private float estimatedGpuBufferMb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphParticleBudgetController>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphParticleBudgetController>();
    }

    private void Awake()
    {
        AutoFindReferences();
        SyncFromSettings();
    }

    private void Start()
    {
        AutoFindReferences();
        SyncFromSettings();

        if (applyHighScalePreparationOnStart)
        {
            ApplyHighScalePreparation(false);
        }
    }

    private void AutoFindReferences()
    {
        if (performanceOptimizer == null) performanceOptimizer = Object.FindFirstObjectByType<SimulationPerformanceOptimizer>();
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (legacyParticleSimulator == null) legacyParticleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (fluidFilmSolver == null) fluidFilmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        if (bucketLiquid == null) bucketLiquid = Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
        if (surfaceBridge == null) surfaceBridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
    }

    private void SyncFromSettings()
    {
        if (settings == null)
        {
            SanitizeInlineFields();
            return;
        }

        settings.Sanitize();
        targetParticleCount = settings.targetParticleCount;
        renderStride = settings.renderStride;
        collisionBatchSize = settings.collisionBatchSize;
        legacyCpuFallbackParticles = settings.legacyCpuFallbackParticles;
        legacyVisualDroplets = settings.legacyVisualDroplets;
        SanitizeInlineFields();
    }

    private void SanitizeInlineFields()
    {
        targetParticleCount = Mathf.Clamp(targetParticleCount, 1024, 4000000);
        renderStride = Mathf.Clamp(renderStride, 1, 256);
        collisionBatchSize = Mathf.Clamp(collisionBatchSize, 128, 131072);
        legacyCpuFallbackParticles = Mathf.Clamp(legacyCpuFallbackParticles, 64, 3000);
        legacyVisualDroplets = Mathf.Clamp(legacyVisualDroplets, 0, 512);
    }

    public void ApplyHighScalePreparation(bool forceOptimizerProfile)
    {
        AutoFindReferences();
        SyncFromSettings();

        if (forceOptimizerProfile && performanceOptimizer != null)
        {
            performanceOptimizer.ApplyProfile(SimulationPerformanceOptimizer.PerformanceMode.UltraSPHPreparation);
        }

        // The legacy particle layer is intentionally kept tiny here. The million-particle target must run on GPU SPH,
        // not through one GameObject or CPU list entry per droplet.
        if (legacyParticleSimulator != null)
        {
            legacyParticleSimulator.ApplyPerformanceBudget(
                legacyCpuFallbackParticles,
                legacyVisualDroplets,
                0,
                false,
                legacyVisualDroplets > 0
            );
            legacyParticleSimulator.autoCullWhenOverBudget = true;
        }

        if (paintEmitter != null)
        {
            paintEmitter.ApplyPerformanceBudget(0.12f, 36f, 40, 18f);
            paintEmitter.enableInternalSlosh = true;
            paintEmitter.flowNoiseAmount = Mathf.Min(paintEmitter.flowNoiseAmount, 0.18f);
        }

        if (canvasPainter != null)
        {
            canvasPainter.fluidTextureUpdateInterval = Mathf.Max(canvasPainter.fluidTextureUpdateInterval, 0.075f);
            canvasPainter.generateFluidNormalMap = true;
            canvasPainter.fluidNormalStrength = Mathf.Clamp(canvasPainter.fluidNormalStrength, 2.0f, 4.0f);
            canvasPainter.fluidThicknessVisibility = Mathf.Clamp(canvasPainter.fluidThicknessVisibility, 0.35f, 0.62f);
        }

        if (fluidFilmSolver != null)
        {
            fluidFilmSolver.enableFluidFilm = true;
            fluidFilmSolver.solveWholeFilm = false;
            fluidFilmSolver.solverInterval = Mathf.Max(fluidFilmSolver.solverInterval, 0.075f);
            fluidFilmSolver.subSteps = Mathf.Min(fluidFilmSolver.subSteps, 2);
            fluidFilmSolver.maxCellsPerFrame = Mathf.Clamp(fluidFilmSolver.maxCellsPerFrame, 6000, 16000);
        }

        if (bucketLiquid != null)
        {
            bucketLiquid.ApplyQualityBudget(64, 14, 6, 8, 0.085f, false, true);
            bucketLiquid.renderLiquidVolume = false;
            bucketLiquid.renderWetInnerWall = true;
            bucketLiquid.renderMeniscus = true;
        }

        if (surfaceBridge != null)
        {
            surfaceBridge.maxImpactsPerFrame = Mathf.Min(surfaceBridge.maxImpactsPerFrame, collisionBatchSize);
            surfaceBridge.highScaleMode = true;
        }

        appliedVisibleParticleBudget = Mathf.Max(1, Mathf.CeilToInt(targetParticleCount / Mathf.Max(1f, renderStride)));
        estimatedGpuBufferMb = EstimateGpuBufferMegabytes();
        highScalePreparationApplied = true;
        lastReadinessSummary = BuildReadinessSummary();

        if (logReadinessSummary)
        {
            Debug.Log("[VR SPH Ready] " + lastReadinessSummary);
        }
    }

    public float EstimateGpuBufferMegabytes()
    {
        float particleMb = targetParticleCount * 96f / (1024f * 1024f);
        float gridResolution = settings != null ? settings.spatialGridResolution : 192f;
        float gridMb = gridResolution * gridResolution * gridResolution * 8f / (1024f * 1024f);
        float renderMb = appliedVisibleParticleBudget * 32f / (1024f * 1024f);
        return particleMb + gridMb + renderMb;
    }

    public string BuildReadinessSummary()
    {
        return "High-scale SPH prepared | target " + targetParticleCount.ToString("N0") +
               " particles | render stride " + renderStride +
               " => visible ~" + appliedVisibleParticleBudget.ToString("N0") +
               " | collision batch " + collisionBatchSize.ToString("N0") +
               " | legacy CPU fallback " + legacyCpuFallbackParticles +
               " | estimated GPU buffers " + estimatedGpuBufferMb.ToString("0.0") + " MB";
    }

    public void ResetSphReadinessRuntimeStats()
    {
        highScalePreparationApplied = false;
        appliedVisibleParticleBudget = 0;
        estimatedGpuBufferMb = 0f;
        lastReadinessSummary = "reset; preparation can be applied again";
    }

    public bool HighScalePreparationApplied => highScalePreparationApplied;
    public string LastReadinessSummary => lastReadinessSummary;
    public int AppliedVisibleParticleBudget => appliedVisibleParticleBudget;
    public float EstimatedGpuBufferMb => estimatedGpuBufferMb;
}
