using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(84)]
public class Phase05WetPaintImpactQualityTuner : MonoBehaviour
{
    private const string RuntimeObjectName = "Phase05WetPaintImpactQualityTuner_Runtime";

    [Header("Auto Apply")]
    public bool applyOnStart = true;
    public bool keepAppliedDuringPlay = true;
    public float applyDelay = 0.35f;

    [Header("References")]
    public CanvasPainter canvasPainter;
    public AdvancedSplatGeneratorV2 splatGenerator;
    public PaintImpactEngineV2 impactEngine;
    public PaintSurfaceStateV2 surfaceState;
    public PaintFilmFluidSolverV2 filmSolver;
    public PaintDripSolverV2 dripSolver;
    public SphCollisionSurfaceBridge collisionBridge;
    public SphPhase05ImpactDirector impactDirector;

    [Header("Quality Target")]
    public bool wetPresentationLook = true;
    [Range(0f, 1f)] public float surfaceSprayReduction = 0.78f;
    [Range(0f, 1f)] public float wetGlossBoost = 0.42f;
    [Range(0f, 1f)] public float cohesiveBlobBoost = 0.62f;

    [Header("Runtime Stats")]
    [SerializeField] private string statusLine = "Phase05 tuner waiting.";
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<Phase05WetPaintImpactQualityTuner>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<Phase05WetPaintImpactQualityTuner>();
    }

    private IEnumerator Start()
    {
        AutoFindReferences();
        if (applyOnStart)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, applyDelay));
            ApplyQualityTuning();
        }
    }

    private void Update()
    {
        if (!keepAppliedDuringPlay)
        {
            return;
        }

        if (Time.unscaledTime < nextRefresh)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + 1.25f;
        ApplyQualityTuning();
    }

    private void AutoFindReferences()
    {
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (splatGenerator == null) splatGenerator = Object.FindFirstObjectByType<AdvancedSplatGeneratorV2>();
        if (impactEngine == null) impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        if (surfaceState == null) surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        if (filmSolver == null) filmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        if (dripSolver == null) dripSolver = Object.FindFirstObjectByType<PaintDripSolverV2>();
        if (collisionBridge == null) collisionBridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        if (impactDirector == null) impactDirector = Object.FindFirstObjectByType<SphPhase05ImpactDirector>();
    }

    public void ApplyQualityTuning()
    {
        AutoFindReferences();

        if (canvasPainter != null)
        {
            canvasPainter.paintOpacity = Mathf.Clamp01(Mathf.Max(canvasPainter.paintOpacity, 0.82f));
            canvasPainter.edgeIrregularity = Mathf.Clamp(canvasPainter.edgeIrregularity, 0.28f, 0.55f);
            canvasPainter.sprayAmount = Mathf.Min(canvasPainter.sprayAmount, Mathf.Lerp(0.18f, 0.05f, surfaceSprayReduction));
            canvasPainter.spraySpread = Mathf.Clamp(canvasPainter.spraySpread, 1.15f, 1.75f);
            canvasPainter.enableDirectionalSmear = true;
            canvasPainter.smearLength = Mathf.Clamp(canvasPainter.smearLength, 0.65f, 1.15f);
            canvasPainter.smearSteps = Mathf.Clamp(canvasPainter.smearSteps, 3, 5);
            canvasPainter.enableWetPaintShading = true;
            canvasPainter.wetCenterDarkening = Mathf.Clamp(Mathf.Max(canvasPainter.wetCenterDarkening, 0.13f), 0.05f, 0.22f);
            canvasPainter.raisedRimHighlight = Mathf.Clamp(Mathf.Max(canvasPainter.raisedRimHighlight, 0.22f), 0.08f, 0.34f);
            canvasPainter.pigmentVariation = Mathf.Min(canvasPainter.pigmentVariation, 0.035f);
            canvasPainter.glossyWetAlphaBoost = Mathf.Clamp(Mathf.Max(canvasPainter.glossyWetAlphaBoost, wetGlossBoost * 0.45f), 0.12f, 0.32f);
            canvasPainter.renderFluidFilmFromSurfaceState = true;
            canvasPainter.fluidTextureUpdateInterval = Mathf.Max(canvasPainter.fluidTextureUpdateInterval, 0.04f);
            canvasPainter.fluidThicknessVisibility = Mathf.Clamp(Mathf.Max(canvasPainter.fluidThicknessVisibility, 0.72f), 0.45f, 0.9f);
            canvasPainter.fluidWetAlpha = Mathf.Clamp(Mathf.Max(canvasPainter.fluidWetAlpha, 0.93f), 0.75f, 0.98f);
            canvasPainter.fluidDryAlpha = Mathf.Clamp(canvasPainter.fluidDryAlpha, 0.34f, 0.56f);
            canvasPainter.fluidSpecularStrength = Mathf.Clamp(Mathf.Max(canvasPainter.fluidSpecularStrength, 0.36f), 0.18f, 0.55f);
            canvasPainter.fluidNormalStrength = Mathf.Clamp(Mathf.Max(canvasPainter.fluidNormalStrength, 3.9f), 2.0f, 5.5f);
            canvasPainter.generateFluidNormalMap = true;
        }

        if (splatGenerator != null)
        {
            splatGenerator.baseBlobMultiplier = Mathf.Lerp(1.05f, 1.45f, cohesiveBlobBoost);
            splatGenerator.heavyBlobMultiplier = Mathf.Lerp(1.65f, 2.15f, cohesiveBlobBoost);
            splatGenerator.hardSplashMultiplier = Mathf.Lerp(0.95f, 1.20f, cohesiveBlobBoost);
            splatGenerator.mistMultiplier = 0.18f;
            splatGenerator.baseSatelliteCount = 2;
            splatGenerator.hardSplashSatelliteCount = 7;
            splatGenerator.mistSatelliteCount = 5;
            splatGenerator.satelliteMinDistance = 0.85f;
            splatGenerator.satelliteMaxDistance = 2.65f;
            splatGenerator.satelliteRadiusMin = 0.08f;
            splatGenerator.satelliteRadiusMax = 0.26f;
            splatGenerator.baseFilamentCount = 2;
            splatGenerator.hardSplashFilamentCount = 4;
            splatGenerator.filamentLengthMin = 0.95f;
            splatGenerator.filamentLengthMax = 2.8f;
            splatGenerator.filamentRadiusMultiplier = 0.17f;
            splatGenerator.smearSteps = 4;
            splatGenerator.grazingSmearLength = 2.7f;
            splatGenerator.skidSmearLength = 3.4f;
            splatGenerator.enableWetHalo = true;
            splatGenerator.wetHaloRadiusMultiplier = 1.42f;
            splatGenerator.wetHaloOpacityMultiplier = 0.18f;
            splatGenerator.maxGeneratedMarksPerImpact = 18;
        }

        if (impactEngine != null)
        {
            impactEngine.useLegacyCanvasOutput = true;
            impactEngine.applySurfaceProfileToLegacyCanvas = true;
            impactEngine.logImpactTypes = false;
        }

        if (surfaceState != null)
        {
            // Do not change mapResolution at runtime because PaintSurfaceStateV2 allocates its maps during Awake.
            surfaceState.thicknessScale = Mathf.Clamp(Mathf.Max(surfaceState.thicknessScale, 72f), 45f, 95f);
            surfaceState.wetnessDepositScale = Mathf.Clamp(Mathf.Max(surfaceState.wetnessDepositScale, 1.2f), 0.7f, 1.7f);
            surfaceState.flowMemory = Mathf.Clamp(surfaceState.flowMemory, 0.62f, 0.82f);
            surfaceState.baseDryingStrength = Mathf.Min(surfaceState.baseDryingStrength, 0.12f);
            surfaceState.absorptionWetnessLoss = Mathf.Min(surfaceState.absorptionWetnessLoss, 0.24f);
            surfaceState.raisedRimMassStrength = Mathf.Clamp(Mathf.Max(surfaceState.raisedRimMassStrength, 0.25f), 0.12f, 0.38f);
            surfaceState.centralMassPower = Mathf.Clamp(surfaceState.centralMassPower, 1.18f, 1.55f);
            surfaceState.maxStableCellThickness = Mathf.Clamp(surfaceState.maxStableCellThickness, 18f, 28f);
        }

        if (filmSolver != null)
        {
            filmSolver.enableFluidFilm = true;
            filmSolver.solveWholeFilm = false;
            filmSolver.solverInterval = Mathf.Clamp(filmSolver.solverInterval, 0.045f, 0.075f);
            filmSolver.subSteps = Mathf.Clamp(filmSolver.subSteps, 1, 2);
            filmSolver.maxCellsPerFrame = Mathf.Clamp(filmSolver.maxCellsPerFrame, 12000, 26000);
            filmSolver.viscosity = Mathf.Clamp(Mathf.Max(filmSolver.viscosity, 6.6f), 4.8f, 9.0f);
            filmSolver.pressureStrength = Mathf.Clamp(filmSolver.pressureStrength, 1.6f, 2.6f);
            filmSolver.gravityStrength = Mathf.Clamp(filmSolver.gravityStrength, 1.7f, 2.8f);
            filmSolver.surfaceTension = Mathf.Clamp(Mathf.Max(filmSolver.surfaceTension, 0.55f), 0.35f, 0.9f);
            filmSolver.adhesion = Mathf.Clamp(Mathf.Max(filmSolver.adhesion, 0.42f), 0.25f, 0.68f);
            filmSolver.wetFriction = Mathf.Clamp(Mathf.Max(filmSolver.wetFriction, 0.64f), 0.44f, 0.82f);
            filmSolver.capillarySpread = Mathf.Clamp(filmSolver.capillarySpread, 0.08f, 0.22f);
        }

        if (dripSolver != null)
        {
            dripSolver.enableDrips = true;
            dripSolver.solverInterval = Mathf.Clamp(dripSolver.solverInterval, 0.07f, 0.12f);
            dripSolver.minThicknessForFlow = Mathf.Clamp(Mathf.Max(dripSolver.minThicknessForFlow, 0.20f), 0.12f, 0.32f);
            dripSolver.baseFlowRate = Mathf.Clamp(dripSolver.baseFlowRate, 0.12f, 0.24f);
            dripSolver.thickPaintDrag = Mathf.Clamp(Mathf.Max(dripSolver.thickPaintDrag, 0.68f), 0.45f, 0.85f);
            dripSolver.lateralSpreadChance = Mathf.Min(dripSolver.lateralSpreadChance, 0.08f);
            dripSolver.visualEveryNTransfers = Mathf.Clamp(dripSolver.visualEveryNTransfers, 4, 8);
            dripSolver.visualRadiusMultiplier = Mathf.Clamp(dripSolver.visualRadiusMultiplier, 0.012f, 0.023f);
        }

        if (collisionBridge != null)
        {
            collisionBridge.highScaleMode = true;
            collisionBridge.maxImpactsPerFrame = Mathf.Min(collisionBridge.maxImpactsPerFrame, 1536);
            collisionBridge.coalesceNearbyHitsEveryN = 1;
            collisionBridge.minimumImpactRadius = Mathf.Clamp(collisionBridge.minimumImpactRadius, 0.008f, 0.02f);
            collisionBridge.maximumImpactRadius = Mathf.Clamp(Mathf.Max(collisionBridge.maximumImpactRadius, 0.11f), 0.08f, 0.16f);
        }

        if (impactDirector != null)
        {
            impactDirector.enablePhase05Impacts = true;
            impactDirector.maxMacroImpactsPerFrame = Mathf.Clamp(impactDirector.maxMacroImpactsPerFrame, 3, 6);
            impactDirector.baseImpactRadius = Mathf.Clamp(Mathf.Max(impactDirector.baseImpactRadius, 0.052f), 0.035f, 0.075f);
            impactDirector.maxImpactRadius = Mathf.Clamp(Mathf.Max(impactDirector.maxImpactRadius, 0.125f), 0.09f, 0.16f);
            impactDirector.satelliteProbability = Mathf.Clamp(impactDirector.satelliteProbability, 0.20f, 0.48f);
        }

        statusLine = "Phase05 wet impact tuning active";
    }

    public string StatusLine => statusLine;
}
