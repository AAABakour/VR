using UnityEngine;

[DefaultExecutionOrder(86)]
public class SphPhase05ImpactDirector : MonoBehaviour
{
    private const string RuntimeObjectName = "SphPhase05ImpactDirector_Runtime";

    [Header("References")]
    public RealGpuSphController gpuSphController;
    public SphCollisionSurfaceBridge collisionBridge;
    public CanvasPainter canvasPainter;
    public Transform bucketTransform;
    public Transform surfacePlane;

    [Header("Phase 05 Macro Impact Model")]
    public bool enablePhase05Impacts = true;
    public bool onlyWhenGpuSphRuntimeEnabled = true;
    [Range(0.0f, 1.0f)] public float pourTiltActivationOffset = 0.035f;
    [Range(1f, 60f)] public float minMacroImpactsPerSecond = 5f;
    [Range(1f, 90f)] public float maxMacroImpactsPerSecond = 28f;
    [Range(1, 12)] public int maxMacroImpactsPerFrame = 5;
    [Range(0f, 1f)] public float satelliteProbability = 0.42f;
    [Range(0f, 5f)] public float impactJitterMeters = 0.055f;
    [Range(0.005f, 0.18f)] public float baseImpactRadius = 0.055f;
    [Range(0.01f, 0.25f)] public float maxImpactRadius = 0.14f;
    [Range(0.01f, 1.0f)] public float paintWetness = 0.92f;
    [Range(0.1f, 12.0f)] public float paintViscosity = 5.8f;
    [Range(0.0f, 2.0f)] public float impactVelocityScale = 0.85f;

    [Header("Surface Projection")]
    public float surfaceYOffset = 0.022f;
    public float forwardPourBias = 0.42f;
    public float downwardPourBias = 1.0f;
    public float minimumFallTime = 0.12f;

    [Header("Runtime Stats")]
    [SerializeField] private int macroImpactsLastFrame;
    [SerializeField] private int totalMacroImpacts;
    [SerializeField] private string statusLine = "Phase05 impact director waiting for GPU SPH.";

    private Vector3 previousBucketPosition;
    private Vector3 bucketVelocity;
    private bool hasBucketState;
    private float impactAccumulator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphPhase05ImpactDirector>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphPhase05ImpactDirector>();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        macroImpactsLastFrame = 0;

        if (!enablePhase05Impacts)
        {
            statusLine = "Phase05 macro impacts disabled.";
            return;
        }

        AutoFindReferences();
        UpdateBucketVelocity();

        if (gpuSphController == null || collisionBridge == null || bucketTransform == null)
        {
            statusLine = "Phase05 impact director waiting for controller / bridge / bucket.";
            return;
        }

        if (onlyWhenGpuSphRuntimeEnabled && !gpuSphController.enableRuntime)
        {
            impactAccumulator = 0f;
            statusLine = "Phase05 impact director armed. Press F9 to run GPU SPH.";
            return;
        }

        float tilt01 = EstimateBucketTilt01();
        float activation = Mathf.Clamp01((tilt01 - (gpuSphController.pourTiltThreshold + pourTiltActivationOffset)) / 0.22f);

        if (activation <= 0.001f)
        {
            impactAccumulator = Mathf.Min(impactAccumulator, 0.25f);
            statusLine = "Phase05 impact director: no pour detected | tilt " + tilt01.ToString("0.00");
            return;
        }

        float fill = Mathf.Clamp01(gpuSphController.initialFill01);
        float flow01 = Mathf.Clamp01(activation * Mathf.Lerp(0.35f, 1.0f, fill));
        float rate = Mathf.Lerp(minMacroImpactsPerSecond, maxMacroImpactsPerSecond, flow01);
        impactAccumulator += Time.deltaTime * rate;

        int count = Mathf.Min(maxMacroImpactsPerFrame, Mathf.FloorToInt(impactAccumulator));
        if (count <= 0)
        {
            statusLine = "Phase05 impact director: building impact flow | rate " + rate.ToString("0.0") + "/s";
            return;
        }

        impactAccumulator -= count;

        for (int i = 0; i < count; i++)
        {
            SubmitMacroImpact(flow01, i, count);
        }

        totalMacroImpacts += macroImpactsLastFrame;
        statusLine = "Phase05 cohesive impacts " + macroImpactsLastFrame + "/frame | total " + totalMacroImpacts + " | flow " + flow01.ToString("0.00");
    }

    private void AutoFindReferences()
    {
        if (gpuSphController == null) gpuSphController = Object.FindFirstObjectByType<RealGpuSphController>();
        if (collisionBridge == null) collisionBridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();

        if (bucketTransform == null)
        {
            if (gpuSphController != null && gpuSphController.bucketTransform != null)
            {
                bucketTransform = gpuSphController.bucketTransform;
            }
            else
            {
                GameObject bucket = GameObject.Find("Bucket");
                if (bucket != null) bucketTransform = bucket.transform;
            }
        }

        if (surfacePlane == null)
        {
            if (gpuSphController != null && gpuSphController.surfacePlane != null)
            {
                surfacePlane = gpuSphController.surfacePlane;
            }
            else if (canvasPainter != null)
            {
                surfacePlane = canvasPainter.transform;
            }
        }
    }

    private void UpdateBucketVelocity()
    {
        if (bucketTransform == null)
        {
            bucketVelocity = Vector3.zero;
            return;
        }

        Vector3 position = bucketTransform.position;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        if (!hasBucketState)
        {
            previousBucketPosition = position;
            bucketVelocity = Vector3.zero;
            hasBucketState = true;
            return;
        }

        bucketVelocity = (position - previousBucketPosition) / dt;
        previousBucketPosition = position;
    }

    private float EstimateBucketTilt01()
    {
        if (bucketTransform == null)
        {
            return 0f;
        }

        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity : new Vector3(0f, -9.81f, 0f);
        Vector3 gravityLocal = bucketTransform.worldToLocalMatrix.MultiplyVector(gravity);
        float horizontal = new Vector2(gravityLocal.x, gravityLocal.z).magnitude;
        return Mathf.Clamp01(horizontal / Mathf.Max(0.001f, gravityLocal.magnitude));
    }

    private Vector2 ResolveDownhillLocalXZ()
    {
        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity : new Vector3(0f, -9.81f, 0f);
        Vector3 gravityLocal = bucketTransform.worldToLocalMatrix.MultiplyVector(gravity);
        Vector2 downhill = new Vector2(gravityLocal.x, gravityLocal.z);
        if (downhill.sqrMagnitude < 0.00001f)
        {
            downhill = Vector2.right;
        }
        return downhill.normalized;
    }

    private void SubmitMacroImpact(float flow01, int index, int count)
    {
        if (collisionBridge == null || bucketTransform == null)
        {
            return;
        }

        Vector2 downhill = ResolveDownhillLocalXZ();
        float rimRadius = Mathf.Max(0.02f, gpuSphController.bucketRadiusLocal * 0.92f);
        Vector3 localRim = new Vector3(downhill.x * rimRadius, gpuSphController.bucketRimLocalY, downhill.y * rimRadius);
        Vector3 rimWorld = bucketTransform.TransformPoint(localRim);

        Vector3 worldDownhill = bucketTransform.TransformDirection(new Vector3(downhill.x, 0f, downhill.y)).normalized;
        Vector3 pourDirection = (worldDownhill * forwardPourBias + Vector3.down * downwardPourBias + bucketVelocity * 0.035f).normalized;
        if (pourDirection.sqrMagnitude < 0.001f)
        {
            pourDirection = Vector3.down;
        }

        float surfaceY = ResolveSurfaceY();
        float denominator = Mathf.Abs(pourDirection.y) < 0.02f ? -0.02f : pourDirection.y;
        float t = (surfaceY - rimWorld.y) / denominator;
        t = Mathf.Clamp(t, 0.15f, 3.0f);

        Vector3 baseHit = rimWorld + pourDirection * t;
        Vector3 tangentA = surfacePlane != null ? surfacePlane.right : Vector3.right;
        Vector3 tangentB = surfacePlane != null ? surfacePlane.forward : Vector3.forward;
        float randomRadius = impactJitterMeters * Mathf.Lerp(0.35f, 1.0f, flow01);
        float angle = Random.value * Mathf.PI * 2f;
        float distance = Mathf.Sqrt(Random.value) * randomRadius;
        Vector3 jitter = tangentA * Mathf.Cos(angle) * distance + tangentB * Mathf.Sin(angle) * distance;
        Vector3 hit = baseHit + jitter;
        hit.y = surfaceY;

        float fallTime = Mathf.Max(minimumFallTime, Mathf.Sqrt(Mathf.Max(0.05f, rimWorld.y - surfaceY) / 9.81f));
        Vector3 incomingVelocity = ((hit - rimWorld) / fallTime + bucketVelocity * 0.35f) * impactVelocityScale;
        incomingVelocity.y = Mathf.Min(incomingVelocity.y, -1.0f - flow01 * 3.5f);

        float radius = Mathf.Clamp(baseImpactRadius * Mathf.Lerp(0.72f, 1.55f, flow01) * Random.Range(0.82f, 1.22f), 0.008f, maxImpactRadius);
        float mass = Mathf.Lerp(0.0035f, 0.021f, flow01) * Random.Range(0.75f, 1.25f);
        Color color = gpuSphController.paintColor;
        color.a = Mathf.Clamp01(Mathf.Lerp(0.82f, 1.0f, flow01));

        collisionBridge.SubmitSingleImpact(hit, Vector3.up, incomingVelocity, color, radius, mass, paintViscosity, paintWetness);
        macroImpactsLastFrame++;

        if (Random.value < satelliteProbability * flow01)
        {
            Vector3 side = (tangentA * Random.Range(-1f, 1f) + tangentB * Random.Range(-1f, 1f)).normalized;
            Vector3 satelliteHit = hit + side * Random.Range(radius * 0.8f, radius * 2.4f);
            satelliteHit.y = surfaceY;
            Vector3 satelliteVelocity = Vector3.Lerp(incomingVelocity, side * incomingVelocity.magnitude, 0.28f);
            collisionBridge.SubmitSingleImpact(satelliteHit, Vector3.up, satelliteVelocity, color, radius * Random.Range(0.24f, 0.48f), mass * 0.22f, paintViscosity, paintWetness * 0.72f);
            macroImpactsLastFrame++;
        }
    }

    private float ResolveSurfaceY()
    {
        if (surfacePlane != null)
        {
            return surfacePlane.position.y + surfaceYOffset;
        }

        if (gpuSphController != null)
        {
            return gpuSphController.surfacePlaneYOffset;
        }

        return surfaceYOffset;
    }

    public int MacroImpactsLastFrame => macroImpactsLastFrame;
    public int TotalMacroImpacts => totalMacroImpacts;
    public string StatusLine => statusLine;
}
