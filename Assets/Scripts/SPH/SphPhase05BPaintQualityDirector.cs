using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(92)]
public class SphPhase05BPaintQualityDirector : MonoBehaviour
{
    private const string RuntimeObjectName = "SphPhase05BPaintQualityDirector_Runtime";
    private const string DepositRootName = "SPH_Phase05B_RaisedWetPaintDeposits";

    [Header("Runtime Control")]
    public bool enablePhase05B = true;
    public bool keepQualityLocked = true;
    public bool onlyWhileGpuSphRuns = true;
    public bool enableControlledPourDeposits = true;
    public bool enableRaisedThicknessVisuals = true;

    [Header("References")]
    public RealGpuSphController gpuSphController;
    public SphHeroPaintVisualRenderer heroVisualRenderer;
    public SphPhase05ImpactDirector impactDirector;
    public SphCollisionSurfaceBridge collisionBridge;
    public PaintImpactEngineV2 impactEngine;
    public PaintSurfaceStateV2 surfaceState;
    public CanvasPainter canvasPainter;
    public PaintEmitter paintEmitter;
    public Transform bucketTransform;
    public Renderer bucketRenderer;
    public Transform surfacePlane;

    [Header("Cohesive Pour")]
    [Range(0.15f, 0.90f)] public float pourTiltThreshold = 0.32f;
    [Range(0.0f, 0.40f)] public float pourHysteresis = 0.055f;
    [Range(0.0f, 1.0f)] public float downhillBias = 0.52f;
    [Range(0.0f, 1.0f)] public float gravityBias = 0.85f;
    [Range(0.0f, 0.12f)] public float targetJitterMeters = 0.018f;
    [Range(0.1f, 3.5f)] public float fallVelocityScale = 1.85f;
    [Range(0.0f, 0.05f)] public float surfaceYOffset = 0.026f;

    [Header("Raised Wet Thickness")]
    [Range(4, 96)] public int maxRaisedDeposits = 36;
    [Range(8, 96)] public int depositMeshSides = 48;
    [Range(0.02f, 0.35f)] public float minDepositRadius = 0.055f;
    [Range(0.04f, 0.60f)] public float maxDepositRadius = 0.26f;
    [Range(0.001f, 0.055f)] public float minDepositHeight = 0.006f;
    [Range(0.004f, 0.090f)] public float maxDepositHeight = 0.038f;
    [Range(0.1f, 3.0f)] public float depositGrowthPerSecond = 0.88f;
    [Range(0.0f, 1.0f)] public float mergeExistingProbability = 0.85f;
    [Range(0.02f, 0.40f)] public float mergeDistance = 0.20f;
    [Range(0.1f, 12.0f)] public float paintViscosity = 7.2f;
    [Range(0.0f, 1.0f)] public float paintWetness = 0.98f;
    public Color thickPaintColor = new Color(0.42f, 0.0025f, 0.0015f, 0.98f);

    [Header("Impact Injection")]
    [Range(0f, 36f)] public float heavyBlobImpactsPerSecond = 12.0f;
    [Range(0f, 28f)] public float satelliteDripsPerSecond = 9.0f;
    [Range(0.005f, 0.22f)] public float impactRadius = 0.095f;
    [Range(0.001f, 0.180f)] public float impactMass = 0.050f;

    [Header("Runtime Readout")]
    [SerializeField] private bool isPouring;
    [SerializeField] private float tilt01;
    [SerializeField] private float flow01;
    [SerializeField] private int activeDepositCount;
    [SerializeField] private string statusLine = "Phase05B waiting.";

    private class Deposit
    {
        public GameObject obj;
        public Mesh mesh;
        public Vector3 worldCenter;
        public Vector2 radii;
        public float height;
        public float age;
        public float wetness;
        public bool active;
    }

    private readonly Deposit[] deposits = new Deposit[96];
    private GameObject depositRoot;
    private Material depositMaterial;
    private float qualityLockTimer;
    private float heavyImpactAccumulator;
    private float satelliteAccumulator;
    private bool wasPouring;
    private float lastPourStopTime;
    private Vector3 previousBucketPosition;
    private Vector3 bucketVelocity;
    private bool hasBucketState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphPhase05BPaintQualityDirector>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphPhase05BPaintQualityDirector>();
    }

    private void Awake()
    {
        AutoFindReferences();
        EnsureDepositResources();
    }

    private void Start()
    {
        AutoFindReferences();
        EnsureDepositResources();
        ApplyQualityLock();
    }

    private void LateUpdate()
    {
        if (!enablePhase05B)
        {
            statusLine = "Phase05B disabled.";
            return;
        }

        AutoFindReferences();
        EnsureDepositResources();
        UpdateBucketMotion(Time.deltaTime);

        if (keepQualityLocked)
        {
            qualityLockTimer -= Time.deltaTime;
            if (qualityLockTimer <= 0f)
            {
                qualityLockTimer = 0.65f;
                ApplyQualityLock();
            }
        }

        bool gpuOk = gpuSphController == null || gpuSphController.enableRuntime || !onlyWhileGpuSphRuns;
        if (!gpuOk)
        {
            isPouring = false;
            statusLine = "Phase05B armed. Press F9 to start controlled cohesive pour.";
            UpdateDepositAging(false);
            return;
        }

        PourFrame frame;
        bool hasFrame = BuildPourFrame(out frame);
        isPouring = hasFrame && frame.pouring;

        if (isPouring)
        {
            heavyImpactAccumulator += Time.deltaTime * heavyBlobImpactsPerSecond * Mathf.Lerp(0.35f, 1f, frame.flow01);
            satelliteAccumulator += Time.deltaTime * satelliteDripsPerSecond * frame.flow01;

            if (enableControlledPourDeposits)
            {
                while (heavyImpactAccumulator >= 1f)
                {
                    heavyImpactAccumulator -= 1f;
                    AddOrGrowDeposit(frame, false);
                    SubmitHeavyBlobImpact(frame, false);
                }

                while (satelliteAccumulator >= 1f)
                {
                    satelliteAccumulator -= 1f;
                    AddOrGrowDeposit(frame, true);
                    SubmitHeavyBlobImpact(frame, true);
                }
            }
        }
        else
        {
            heavyImpactAccumulator = Mathf.Min(heavyImpactAccumulator, 0.8f);
            satelliteAccumulator = Mathf.Min(satelliteAccumulator, 0.6f);
            if (wasPouring)
            {
                lastPourStopTime = Time.time;
            }
        }

        wasPouring = isPouring;
        UpdateDepositAging(isPouring);
        statusLine = "Phase05B cohesive pour | pouring " + isPouring + " | flow " + flow01.ToString("0.00") + " | raised deposits " + activeDepositCount;
    }

    private void AutoFindReferences()
    {
        if (gpuSphController == null) gpuSphController = Object.FindFirstObjectByType<RealGpuSphController>();
        if (heroVisualRenderer == null) heroVisualRenderer = Object.FindFirstObjectByType<SphHeroPaintVisualRenderer>();
        if (impactDirector == null) impactDirector = Object.FindFirstObjectByType<SphPhase05ImpactDirector>();
        if (collisionBridge == null) collisionBridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        if (impactEngine == null) impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        if (surfaceState == null) surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();

        if (bucketTransform == null)
        {
            if (gpuSphController != null && gpuSphController.bucketTransform != null) bucketTransform = gpuSphController.bucketTransform;
            else
            {
                GameObject bucket = GameObject.Find("Bucket");
                if (bucket != null) bucketTransform = bucket.transform;
            }
        }

        if (bucketRenderer == null)
        {
            if (gpuSphController != null && gpuSphController.bucketRenderer != null) bucketRenderer = gpuSphController.bucketRenderer;
            else if (bucketTransform != null) bucketRenderer = bucketTransform.GetComponentInChildren<Renderer>();
        }

        if (surfacePlane == null)
        {
            if (gpuSphController != null && gpuSphController.surfacePlane != null) surfacePlane = gpuSphController.surfacePlane;
            else if (canvasPainter != null) surfacePlane = canvasPainter.transform;
        }
    }

    private void EnsureDepositResources()
    {
        Shader shader = Shader.Find("VR/Paint/Thick Lens Paint URP");
        if (shader == null) shader = Shader.Find("VR/Paint/Hero Paint Surface URP");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (depositMaterial == null)
        {
            depositMaterial = new Material(shader);
            depositMaterial.name = "Runtime_Phase05B_ThickWetPaintLens";
            depositMaterial.renderQueue = 3015;
        }

        SetMaterialColor(depositMaterial, thickPaintColor);

        if (depositRoot == null)
        {
            depositRoot = GameObject.Find(DepositRootName);
            if (depositRoot == null) depositRoot = new GameObject(DepositRootName);
        }

        int count = Mathf.Clamp(maxRaisedDeposits, 1, deposits.Length);
        for (int i = 0; i < count; i++)
        {
            if (deposits[i] != null) continue;
            deposits[i] = CreateDeposit(i);
        }
    }

    private Deposit CreateDeposit(int index)
    {
        GameObject obj = new GameObject("SPH_Phase05B_RaisedWetPaint_" + index.ToString("00"));
        obj.transform.SetParent(depositRoot.transform, true);
        MeshFilter filter = obj.AddComponent<MeshFilter>();
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = depositMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Mesh mesh = new Mesh();
        mesh.name = obj.name + "_Mesh";
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;

        obj.SetActive(false);

        return new Deposit
        {
            obj = obj,
            mesh = mesh,
            worldCenter = Vector3.zero,
            radii = Vector2.zero,
            height = 0f,
            age = 0f,
            wetness = 0f,
            active = false
        };
    }

    private void ApplyQualityLock()
    {
        if (gpuSphController != null)
        {
            gpuSphController.drawGpuParticles = false;
            gpuSphController.showRuntimeOverlay = false;
            gpuSphController.paintColor = thickPaintColor;
            gpuSphController.solverSubsteps = 1;
            gpuSphController.maxSolverDeltaTime = Mathf.Max(gpuSphController.maxSolverDeltaTime, 0.0125f);
            gpuSphController.gridResolutionX = Mathf.Min(gpuSphController.gridResolutionX, 56);
            gpuSphController.gridResolutionY = Mathf.Min(gpuSphController.gridResolutionY, 36);
            gpuSphController.gridResolutionZ = Mathf.Min(gpuSphController.gridResolutionZ, 56);
            gpuSphController.maxParticlesPerCell = Mathf.Min(gpuSphController.maxParticlesPerCell, 6);
            gpuSphController.renderStride = Mathf.Max(gpuSphController.renderStride, 48);
            gpuSphController.renderRadiusScale = Mathf.Max(gpuSphController.renderRadiusScale, 2.05f);
            gpuSphController.viscosityDrag = Mathf.Clamp(gpuSphController.viscosityDrag, 3.0f, 5.0f);
            gpuSphController.cohesionStrength = Mathf.Clamp(gpuSphController.cohesionStrength, 4.2f, 6.4f);
            gpuSphController.spillPush = Mathf.Clamp(gpuSphController.spillPush, 1.35f, 2.35f);
            gpuSphController.maxParticleVelocity = Mathf.Clamp(gpuSphController.maxParticleVelocity, 9.0f, 14.0f);
            gpuSphController.airborneParticleRenderScale = Mathf.Clamp(gpuSphController.airborneParticleRenderScale, 0.38f, 0.85f);
            gpuSphController.depositedParticleRenderScale = Mathf.Min(gpuSphController.depositedParticleRenderScale, 0.025f);
            gpuSphController.containedParticleRenderScale = 0f;
            gpuSphController.pourTiltThreshold = Mathf.Clamp(gpuSphController.pourTiltThreshold, 0.30f, 0.40f);
        }

        if (heroVisualRenderer != null)
        {
            heroVisualRenderer.paintColor = thickPaintColor;
            heroVisualRenderer.streamLengthGain = Mathf.Clamp(heroVisualRenderer.streamLengthGain, 1.05f, 1.45f);
            heroVisualRenderer.streamStartRadius = Mathf.Clamp(heroVisualRenderer.streamStartRadius, 0.050f, 0.078f);
            heroVisualRenderer.streamEndRadius = Mathf.Clamp(heroVisualRenderer.streamEndRadius, 0.056f, 0.088f);
            heroVisualRenderer.streamSegments = Mathf.Clamp(heroVisualRenderer.streamSegments, 20, 30);
            heroVisualRenderer.streamSides = Mathf.Clamp(heroVisualRenderer.streamSides, 8, 12);
            heroVisualRenderer.streamBreakup = Mathf.Clamp(heroVisualRenderer.streamBreakup, 0.010f, 0.020f);
            heroVisualRenderer.streamGloss = Mathf.Clamp(heroVisualRenderer.streamGloss, 0.035f, 0.070f);
            heroVisualRenderer.velocityResponse = Mathf.Clamp(heroVisualRenderer.velocityResponse, 0.25f, 0.55f);
            heroVisualRenderer.dropletCount = Mathf.Clamp(heroVisualRenderer.dropletCount, 40, 72);
            heroVisualRenderer.impactPoolRadius = Mathf.Clamp(heroVisualRenderer.impactPoolRadius, 0.24f, 0.42f);
            heroVisualRenderer.impactSegments = Mathf.Clamp(heroVisualRenderer.impactSegments, 36, 64);
            heroVisualRenderer.visualPourTiltThreshold = Mathf.Clamp(heroVisualRenderer.visualPourTiltThreshold, 0.28f, 0.38f);
            heroVisualRenderer.surfaceYOffset = Mathf.Max(heroVisualRenderer.surfaceYOffset, surfaceYOffset);
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = thickPaintColor;
            paintEmitter.viscosity = Mathf.Max(paintEmitter.viscosity, 2.4f);
            paintEmitter.particlesPerUnitFlow = Mathf.Clamp(paintEmitter.particlesPerUnitFlow, 45f, 95f);
            paintEmitter.maxParticlesEmittedPerFrame = Mathf.Clamp(paintEmitter.maxParticlesEmittedPerFrame, 48, 90);
            paintEmitter.randomSpread = Mathf.Clamp(paintEmitter.randomSpread, 0.025f, 0.060f);
            paintEmitter.flowNoiseAmount = Mathf.Clamp(paintEmitter.flowNoiseAmount, 0.045f, 0.095f);
            paintEmitter.minParticleRadius = Mathf.Clamp(paintEmitter.minParticleRadius, 0.026f, 0.045f);
            paintEmitter.maxParticleRadius = Mathf.Clamp(paintEmitter.maxParticleRadius, 0.058f, 0.105f);
        }

        if (canvasPainter != null)
        {
            canvasPainter.paintOpacity = Mathf.Clamp(canvasPainter.paintOpacity, 0.88f, 0.98f);
            canvasPainter.sprayAmount = Mathf.Min(canvasPainter.sprayAmount, 0.035f);
            canvasPainter.spraySpread = Mathf.Clamp(canvasPainter.spraySpread, 1.0f, 1.25f);
            canvasPainter.edgeIrregularity = Mathf.Clamp(canvasPainter.edgeIrregularity, 0.16f, 0.32f);
            canvasPainter.enableDirectionalSmear = true;
            canvasPainter.smearLength = Mathf.Clamp(canvasPainter.smearLength, 0.70f, 1.10f);
            canvasPainter.smearSteps = Mathf.Clamp(canvasPainter.smearSteps, 3, 5);
            canvasPainter.enableWetPaintShading = true;
            canvasPainter.wetCenterDarkening = Mathf.Clamp(Mathf.Max(canvasPainter.wetCenterDarkening, 0.16f), 0.12f, 0.25f);
            canvasPainter.raisedRimHighlight = Mathf.Clamp(Mathf.Max(canvasPainter.raisedRimHighlight, 0.26f), 0.18f, 0.38f);
            canvasPainter.pigmentVariation = Mathf.Min(canvasPainter.pigmentVariation, 0.025f);
            canvasPainter.glossyWetAlphaBoost = Mathf.Clamp(Mathf.Max(canvasPainter.glossyWetAlphaBoost, 0.24f), 0.18f, 0.36f);
            canvasPainter.fluidThicknessVisibility = Mathf.Clamp(Mathf.Max(canvasPainter.fluidThicknessVisibility, 0.84f), 0.70f, 1.0f);
            canvasPainter.fluidSpecularStrength = Mathf.Clamp(Mathf.Max(canvasPainter.fluidSpecularStrength, 0.44f), 0.30f, 0.62f);
            canvasPainter.fluidNormalStrength = Mathf.Clamp(Mathf.Max(canvasPainter.fluidNormalStrength, 4.2f), 3.0f, 6.0f);
        }

        if (surfaceState != null)
        {
            surfaceState.thicknessScale = Mathf.Clamp(Mathf.Max(surfaceState.thicknessScale, 92f), 72f, 135f);
            surfaceState.wetnessDepositScale = Mathf.Clamp(Mathf.Max(surfaceState.wetnessDepositScale, 1.35f), 1.0f, 1.9f);
            surfaceState.baseDryingStrength = Mathf.Min(surfaceState.baseDryingStrength, 0.07f);
            surfaceState.absorptionWetnessLoss = Mathf.Min(surfaceState.absorptionWetnessLoss, 0.16f);
            surfaceState.raisedRimMassStrength = Mathf.Clamp(Mathf.Max(surfaceState.raisedRimMassStrength, 0.30f), 0.22f, 0.48f);
            surfaceState.centralMassPower = Mathf.Clamp(surfaceState.centralMassPower, 1.08f, 1.34f);
            surfaceState.maxStableCellThickness = Mathf.Clamp(Mathf.Max(surfaceState.maxStableCellThickness, 30f), 24f, 48f);
        }

        if (collisionBridge != null)
        {
            collisionBridge.enableSpatialCoalescing = true;
            collisionBridge.clusterCellSize = Mathf.Clamp(Mathf.Max(collisionBridge.clusterCellSize, 0.070f), 0.055f, 0.105f);
            collisionBridge.clusteredRadiusGain = Mathf.Clamp(Mathf.Max(collisionBridge.clusteredRadiusGain, 0.72f), 0.55f, 1.05f);
            collisionBridge.minimumImpactRadius = Mathf.Clamp(Mathf.Max(collisionBridge.minimumImpactRadius, 0.012f), 0.008f, 0.024f);
            collisionBridge.maximumImpactRadius = Mathf.Clamp(Mathf.Max(collisionBridge.maximumImpactRadius, 0.16f), 0.12f, 0.20f);
            collisionBridge.maxClusteredImpactsPerFrame = Mathf.Clamp(collisionBridge.maxClusteredImpactsPerFrame, 96, 384);
        }

        if (impactDirector != null)
        {
            impactDirector.enablePhase05Impacts = true;
            impactDirector.minMacroImpactsPerSecond = Mathf.Clamp(impactDirector.minMacroImpactsPerSecond, 2.5f, 7.0f);
            impactDirector.maxMacroImpactsPerSecond = Mathf.Clamp(impactDirector.maxMacroImpactsPerSecond, 8.0f, 16.0f);
            impactDirector.maxMacroImpactsPerFrame = Mathf.Clamp(impactDirector.maxMacroImpactsPerFrame, 1, 3);
            impactDirector.satelliteProbability = Mathf.Min(impactDirector.satelliteProbability, 0.12f);
            impactDirector.impactJitterMeters = Mathf.Min(impactDirector.impactJitterMeters, 0.028f);
            impactDirector.baseImpactRadius = Mathf.Clamp(impactDirector.baseImpactRadius, 0.060f, 0.095f);
            impactDirector.maxImpactRadius = Mathf.Clamp(Mathf.Max(impactDirector.maxImpactRadius, 0.15f), 0.12f, 0.19f);
            impactDirector.paintViscosity = Mathf.Max(impactDirector.paintViscosity, paintViscosity);
            impactDirector.paintWetness = Mathf.Max(impactDirector.paintWetness, paintWetness);
        }
    }

    private void UpdateBucketMotion(float dtRaw)
    {
        if (bucketTransform == null)
        {
            bucketVelocity = Vector3.zero;
            hasBucketState = false;
            return;
        }

        float dt = Mathf.Max(dtRaw, 0.0001f);
        if (hasBucketState)
        {
            Vector3 instantaneous = (bucketTransform.position - previousBucketPosition) / dt;
            bucketVelocity = Vector3.Lerp(bucketVelocity, instantaneous, 1f - Mathf.Exp(-dt / 0.08f));
        }
        else
        {
            bucketVelocity = Vector3.zero;
            hasBucketState = true;
        }

        previousBucketPosition = bucketTransform.position;
    }

    private struct PourFrame
    {
        public bool pouring;
        public float flow01;
        public Vector3 source;
        public Vector3 target;
        public Vector3 velocity;
        public Vector3 tangentA;
        public Vector3 tangentB;
        public Quaternion surfaceRotation;
    }

    private bool BuildPourFrame(out PourFrame frame)
    {
        frame = new PourFrame();
        if (bucketTransform == null)
        {
            return false;
        }

        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity.normalized : Vector3.down;
        Vector3 gravityLocal3 = bucketTransform.InverseTransformDirection(gravity);
        Vector2 gravityLocal = new Vector2(gravityLocal3.x, gravityLocal3.z);
        tilt01 = Mathf.Clamp01(gravityLocal.magnitude);

        float threshold = pourTiltThreshold;
        if (gpuSphController != null) threshold = Mathf.Min(threshold, gpuSphController.pourTiltThreshold + 0.02f);
        if (wasPouring) threshold -= pourHysteresis;

        float fill01 = ResolveFill01();
        flow01 = Mathf.Clamp01((tilt01 - threshold) / Mathf.Max(0.001f, 1f - threshold));
        flow01 = Mathf.SmoothStep(0f, 1f, flow01) * Mathf.Lerp(0.30f, 1.0f, fill01);
        frame.pouring = flow01 > 0.025f && fill01 > 0.035f;
        frame.flow01 = flow01;

        Vector2 downhill = gravityLocal.sqrMagnitude > 0.00001f ? gravityLocal.normalized : Vector2.right;
        float rimRadius = ResolveBucketRadius() * 0.92f;
        float rimY = ResolveBucketRimY() + 0.012f;
        frame.source = bucketTransform.TransformPoint(new Vector3(downhill.x * rimRadius, rimY, downhill.y * rimRadius));

        Vector3 worldDownhill = bucketTransform.TransformDirection(new Vector3(downhill.x, 0f, downhill.y)).normalized;
        Vector3 pourDirection = (worldDownhill * downhillBias + Vector3.down * gravityBias + bucketVelocity * 0.025f).normalized;
        if (pourDirection.y > -0.04f) pourDirection = Vector3.Slerp(pourDirection, Vector3.down, 0.72f).normalized;

        float surfaceY = ResolveSurfaceY();
        float denominator = Mathf.Abs(pourDirection.y) < 0.02f ? -0.02f : pourDirection.y;
        float t = Mathf.Clamp((surfaceY - frame.source.y) / denominator, 0.12f, 3.0f);
        frame.target = frame.source + pourDirection * t;
        frame.target.y = surfaceY;

        Vector3 sideA = surfacePlane != null ? surfacePlane.right : Vector3.right;
        Vector3 sideB = surfacePlane != null ? surfacePlane.forward : Vector3.forward;
        Vector2 jitter = Random.insideUnitCircle * targetJitterMeters * Mathf.Lerp(0.25f, 1.0f, flow01);
        frame.target += sideA * jitter.x + sideB * jitter.y;
        frame.target.y = surfaceY;
        frame.tangentA = sideA.normalized;
        frame.tangentB = sideB.normalized;
        frame.surfaceRotation = surfacePlane != null ? surfacePlane.rotation : Quaternion.identity;

        float fallTime = Mathf.Sqrt(Mathf.Max(0.05f, frame.source.y - surfaceY) / 9.81f);
        frame.velocity = ((frame.target - frame.source) / Mathf.Max(0.10f, fallTime) + bucketVelocity * 0.18f) * fallVelocityScale;
        frame.velocity.y = Mathf.Min(frame.velocity.y, -1.4f - flow01 * 3.2f);
        return true;
    }

    private void AddOrGrowDeposit(PourFrame frame, bool satellite)
    {
        if (!enableRaisedThicknessVisuals)
        {
            return;
        }

        Deposit deposit = null;
        Vector3 target = frame.target;
        if (satellite)
        {
            float satelliteRadius = Random.Range(impactRadius * 1.0f, impactRadius * 2.3f);
            Vector2 offset = Random.insideUnitCircle.normalized * satelliteRadius;
            target += frame.tangentA * offset.x + frame.tangentB * offset.y;
            target.y = ResolveSurfaceY();
        }

        if (Random.value < mergeExistingProbability)
        {
            deposit = FindNearestActiveDeposit(target, mergeDistance * Mathf.Lerp(0.75f, 1.30f, frame.flow01));
        }

        if (deposit == null)
        {
            deposit = FindFreeOrOldestDeposit();
            if (deposit == null) return;
            deposit.active = true;
            deposit.worldCenter = target;
            float initialRadius = Mathf.Lerp(minDepositRadius, minDepositRadius * 1.55f, frame.flow01) * (satellite ? 0.50f : 1f);
            deposit.radii = new Vector2(initialRadius, initialRadius * Random.Range(0.58f, 0.86f));
            deposit.height = Mathf.Lerp(minDepositHeight, minDepositHeight * 1.9f, frame.flow01) * (satellite ? 0.42f : 1f);
            deposit.age = 0f;
            deposit.wetness = 1f;
            if (deposit.obj != null) deposit.obj.SetActive(true);
        }
        else
        {
            deposit.worldCenter = Vector3.Lerp(deposit.worldCenter, target, satellite ? 0.08f : 0.18f);
            float growth = depositGrowthPerSecond * Time.deltaTime * Mathf.Lerp(0.35f, 1.0f, frame.flow01) * (satellite ? 0.25f : 1f);
            deposit.radii.x = Mathf.Min(maxDepositRadius, deposit.radii.x + growth * 0.075f);
            deposit.radii.y = Mathf.Min(maxDepositRadius * 0.78f, deposit.radii.y + growth * 0.045f);
            deposit.height = Mathf.Min(maxDepositHeight, deposit.height + growth * 0.010f);
            deposit.wetness = 1f;
        }

        Vector3 pathDir = frame.target - frame.source;
        pathDir.y = 0f;
        if (pathDir.sqrMagnitude < 0.001f) pathDir = frame.tangentB;
        float angle = Mathf.Atan2(Vector3.Dot(pathDir.normalized, frame.tangentA), Vector3.Dot(pathDir.normalized, frame.tangentB)) * Mathf.Rad2Deg;
        RebuildDepositMesh(deposit, angle);
    }

    private void SubmitHeavyBlobImpact(PourFrame frame, bool satellite)
    {
        if (collisionBridge == null)
        {
            return;
        }

        Vector3 hit = frame.target;
        if (satellite)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(impactRadius * 1.0f, impactRadius * 2.0f);
            hit += frame.tangentA * offset.x + frame.tangentB * offset.y;
            hit.y = ResolveSurfaceY();
        }

        float r = impactRadius * Mathf.Lerp(0.70f, 1.45f, frame.flow01) * (satellite ? Random.Range(0.32f, 0.52f) : Random.Range(0.88f, 1.12f));
        float m = impactMass * Mathf.Lerp(0.65f, 1.70f, frame.flow01) * (satellite ? 0.22f : 1f);
        Color c = thickPaintColor;
        c.a = Mathf.Clamp01(Mathf.Lerp(0.86f, 1.0f, frame.flow01));
        collisionBridge.SubmitSingleImpact(hit, Vector3.up, frame.velocity, c, r, m, paintViscosity, paintWetness);
    }

    private Deposit FindNearestActiveDeposit(Vector3 point, float maxDistance)
    {
        Deposit best = null;
        float bestSqr = maxDistance * maxDistance;
        int count = Mathf.Clamp(maxRaisedDeposits, 1, deposits.Length);
        for (int i = 0; i < count; i++)
        {
            Deposit d = deposits[i];
            if (d == null || !d.active) continue;
            float sqr = (d.worldCenter - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = d;
            }
        }
        return best;
    }

    private Deposit FindFreeOrOldestDeposit()
    {
        Deposit oldest = null;
        int count = Mathf.Clamp(maxRaisedDeposits, 1, deposits.Length);
        for (int i = 0; i < count; i++)
        {
            Deposit d = deposits[i];
            if (d == null) continue;
            if (!d.active) return d;
            if (oldest == null || d.age > oldest.age) oldest = d;
        }
        return oldest;
    }

    private void RebuildDepositMesh(Deposit deposit, float angleDegrees)
    {
        if (deposit == null || deposit.mesh == null || deposit.obj == null)
        {
            return;
        }

        int sides = Mathf.Clamp(depositMeshSides, 8, 96);
        int rings = 4;
        int vertCount = 1 + rings * sides;
        int triCount = sides * 3 + (rings - 1) * sides * 6;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        int[] triangles = new int[triCount];

        vertices[0] = new Vector3(0f, deposit.height, 0f);
        normals[0] = Vector3.up;
        uv[0] = new Vector2(0.5f, 0.5f);

        float angleRad = angleDegrees * Mathf.Deg2Rad;
        float ca = Mathf.Cos(angleRad);
        float sa = Mathf.Sin(angleRad);

        for (int ring = 1; ring <= rings; ring++)
        {
            float rn = ring / (float)rings;
            float lensHeight = deposit.height * Mathf.Pow(1f - rn, 1.85f);
            float rim = Mathf.Exp(-Mathf.Pow((rn - 0.72f) / 0.18f, 2f)) * deposit.height * 0.26f;
            lensHeight += rim;
            if (ring == rings) lensHeight = 0.0012f;

            for (int side = 0; side < sides; side++)
            {
                float a = side / (float)sides * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(a * 5f + deposit.age * 0.4f) * 0.025f + Mathf.Sin(a * 9f + 1.7f) * 0.018f;
                float x = Mathf.Cos(a) * deposit.radii.x * rn * wobble;
                float z = Mathf.Sin(a) * deposit.radii.y * rn * wobble;
                float rx = x * ca - z * sa;
                float rz = x * sa + z * ca;
                int index = 1 + (ring - 1) * sides + side;
                vertices[index] = new Vector3(rx, lensHeight, rz);
                normals[index] = Vector3.up;
                uv[index] = new Vector2(Mathf.Cos(a) * rn * 0.5f + 0.5f, Mathf.Sin(a) * rn * 0.5f + 0.5f);
            }
        }

        int t = 0;
        for (int side = 0; side < sides; side++)
        {
            triangles[t++] = 0;
            triangles[t++] = 1 + side;
            triangles[t++] = 1 + ((side + 1) % sides);
        }

        for (int ring = 1; ring < rings; ring++)
        {
            int ringStart = 1 + (ring - 1) * sides;
            int nextStart = 1 + ring * sides;
            for (int side = 0; side < sides; side++)
            {
                int a = ringStart + side;
                int b = ringStart + ((side + 1) % sides);
                int c = nextStart + side;
                int d = nextStart + ((side + 1) % sides);
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }

        deposit.mesh.Clear();
        deposit.mesh.vertices = vertices;
        deposit.mesh.normals = normals;
        deposit.mesh.uv = uv;
        deposit.mesh.triangles = triangles;
        deposit.mesh.RecalculateBounds();

        deposit.obj.transform.position = deposit.worldCenter + Vector3.up * 0.0025f;
        deposit.obj.transform.rotation = surfacePlane != null ? surfacePlane.rotation : Quaternion.identity;
        deposit.obj.transform.localScale = Vector3.one;
    }

    private void UpdateDepositAging(bool pouring)
    {
        activeDepositCount = 0;
        int count = Mathf.Clamp(maxRaisedDeposits, 1, deposits.Length);
        for (int i = 0; i < count; i++)
        {
            Deposit d = deposits[i];
            if (d == null || !d.active) continue;
            d.age += Time.deltaTime;
            d.wetness = Mathf.Clamp01(d.wetness - Time.deltaTime * (pouring ? 0.010f : 0.025f));
            activeDepositCount++;
        }
    }

    private float ResolveBucketRadius()
    {
        if (gpuSphController != null) return Mathf.Max(0.04f, gpuSphController.bucketRadiusLocal);
        if (bucketRenderer != null && bucketTransform != null)
        {
            float scale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(bucketTransform.lossyScale.x), Mathf.Abs(bucketTransform.lossyScale.z)));
            return Mathf.Max(0.04f, Mathf.Max(bucketRenderer.bounds.extents.x, bucketRenderer.bounds.extents.z) / scale * 0.58f);
        }
        return 0.28f;
    }

    private float ResolveBucketRimY()
    {
        if (gpuSphController != null) return gpuSphController.bucketRimLocalY;
        return 0.42f;
    }

    private float ResolveFill01()
    {
        if (paintEmitter != null) return Mathf.Clamp01(paintEmitter.PaintFill01);
        if (gpuSphController != null) return Mathf.Clamp01(gpuSphController.initialFill01);
        return 0.70f;
    }

    private float ResolveSurfaceY()
    {
        if (surfacePlane != null) return surfacePlane.position.y + surfaceYOffset;
        if (canvasPainter != null) return canvasPainter.transform.position.y + surfaceYOffset;
        if (gpuSphController != null) return gpuSphController.surfacePlaneYOffset + surfaceYOffset;
        return surfaceYOffset;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Alpha")) material.SetFloat("_Alpha", color.a);
        if (material.HasProperty("_GlossBoost")) material.SetFloat("_GlossBoost", 1.45f);
        if (material.HasProperty("_HeightGloss")) material.SetFloat("_HeightGloss", 1.25f);
    }

    public string StatusLine => statusLine;
}
