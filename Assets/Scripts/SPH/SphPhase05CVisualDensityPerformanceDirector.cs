using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(96)]
public class SphPhase05CVisualDensityPerformanceDirector : MonoBehaviour
{
    private const string RuntimeObjectName = "SphPhase05C_VisualDensityPerformanceDirector_Runtime";
    private const int MaxInstances = 768;
    private const int InstanceBatchSize = 1023;

    [Header("Runtime Control")]
    public bool enablePhase05C = true;
    public bool onlyWhileGpuSphRuns = true;
    public bool lockPresentationPerformance = true;
    public bool protectEditorFps = true;
    public bool enableDenseInstancedDroplets = true;

    [Header("References")]
    public RealGpuSphController gpuSphController;
    public SphHeroPaintVisualRenderer heroVisualRenderer;
    public SphPhase05BPaintQualityDirector phase05BDirector;
    public RuntimeAdaptiveQualityGovernor adaptiveQualityGovernor;
    public SimulationPerformanceOptimizer performanceOptimizer;
    public SphParticleBudgetController sphBudgetController;
    public CanvasPainter canvasPainter;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator legacyParticleSimulator;
    public Transform bucketTransform;
    public Renderer bucketRenderer;

    [Header("Perceived Million-Particle Visual Density")]
    [Range(0, MaxInstances)] public int perceivedDropletCount = 260;
    [Range(0.25f, 4.0f)] public float dropletFallSpeed = 1.65f;
    [Range(0.0f, 0.35f)] public float dropletCloudWidth = 0.16f;
    [Range(0.004f, 0.055f)] public float minDropletScale = 0.010f;
    [Range(0.006f, 0.080f)] public float maxDropletScale = 0.035f;
    [Range(0.0f, 1.0f)] public float microMistFraction = 0.28f;
    [Range(0.12f, 0.90f)] public float visualPourTiltThreshold = 0.30f;
    [Range(0.1f, 2.0f)] public float flowAmplification = 1.35f;
    [Range(0.0f, 0.04f)] public float surfaceYOffset = 0.025f;
    public Color paintColor = new Color(0.42f, 0.0025f, 0.0015f, 0.96f);

    [Header("GPU Solver Guard")]
    [Range(32768, 1000000)] public int defaultSimulationParticles = 262144;
    [Range(131072, 1200000)] public int maxPresentationParticles = 524288;
    [Range(1, 256)] public int normalRenderStride = 64;
    [Range(1, 256)] public int millionRenderStride = 96;
    [Range(8, 160)] public int presentationGridX = 52;
    [Range(8, 128)] public int presentationGridY = 34;
    [Range(8, 160)] public int presentationGridZ = 52;
    [Range(2, 16)] public int presentationCellSlots = 6;
    [Range(15f, 90f)] public float editorFpsProtectionThreshold = 32f;
    [Range(1.0f, 10.0f)] public float lowFpsSecondsBeforeProtect = 3.5f;

    [Header("Runtime Readout")]
    [SerializeField] private bool pouring;
    [SerializeField] private float tilt01;
    [SerializeField] private float flow01;
    [SerializeField] private float smoothedFps;
    [SerializeField] private string statusLine = "Phase05C waiting.";

    private Mesh dropletMesh;
    private Material dropletMaterial;
    private readonly Matrix4x4[] matrices = new Matrix4x4[MaxInstances];
    private float qualityLockTimer;
    private float fpsAccum;
    private int fpsFrames;
    private float fpsWindowTimer;
    private float lowFpsTimer;
    private bool protectedBudgetApplied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphPhase05CVisualDensityPerformanceDirector>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphPhase05CVisualDensityPerformanceDirector>();
    }

    private void Awake()
    {
        AutoFindReferences();
        EnsureRenderResources();
    }

    private void Start()
    {
        AutoFindReferences();
        EnsureRenderResources();
        ApplyPresentationLock(false);
    }

    private void LateUpdate()
    {
        if (!enablePhase05C)
        {
            statusLine = "Phase05C disabled.";
            return;
        }

        AutoFindReferences();
        EnsureRenderResources();
        TrackFps();

        if (lockPresentationPerformance)
        {
            qualityLockTimer -= Time.unscaledDeltaTime;
            if (qualityLockTimer <= 0f)
            {
                qualityLockTimer = 0.55f;
                ApplyPresentationLock(false);
            }
        }

        if (protectEditorFps)
        {
            ProtectFpsIfNeeded();
        }

        bool gpuOk = gpuSphController == null || gpuSphController.enableRuntime || !onlyWhileGpuSphRuns;
        if (!gpuOk || bucketTransform == null)
        {
            pouring = false;
            statusLine = "Phase05C armed. Press F9; visual density is GPU-instanced, not CPU droplets.";
            return;
        }

        PourFrame frame = BuildPourFrame();
        pouring = frame.pouring;

        if (pouring && enableDenseInstancedDroplets && dropletMesh != null && dropletMaterial != null)
        {
            DrawDenseDropletField(frame);
        }

        statusLine = "Phase05C dense pour | flow " + flow01.ToString("0.00") + " | visual droplets " + ActiveDropletCount(frame).ToString() + " | FPS " + smoothedFps.ToString("0.0") + (protectedBudgetApplied ? " | protected budget" : "");
    }

    private void AutoFindReferences()
    {
        if (gpuSphController == null) gpuSphController = Object.FindFirstObjectByType<RealGpuSphController>();
        if (heroVisualRenderer == null) heroVisualRenderer = Object.FindFirstObjectByType<SphHeroPaintVisualRenderer>();
        if (phase05BDirector == null) phase05BDirector = Object.FindFirstObjectByType<SphPhase05BPaintQualityDirector>();
        if (adaptiveQualityGovernor == null) adaptiveQualityGovernor = Object.FindFirstObjectByType<RuntimeAdaptiveQualityGovernor>();
        if (performanceOptimizer == null) performanceOptimizer = Object.FindFirstObjectByType<SimulationPerformanceOptimizer>();
        if (sphBudgetController == null) sphBudgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (legacyParticleSimulator == null) legacyParticleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();

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
    }

    private void EnsureRenderResources()
    {
        if (dropletMesh == null)
        {
            dropletMesh = CreateOctaDropletMesh();
        }

        if (dropletMaterial == null)
        {
            Shader shader = Shader.Find("VR/Paint/Phase05C Instanced Droplet URP");
            if (shader == null) shader = Shader.Find("VR/Paint/Hero Paint Surface URP");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            dropletMaterial = new Material(shader);
            dropletMaterial.name = "Runtime_Phase05C_DenseInstancedDroplets";
            dropletMaterial.enableInstancing = true;
            dropletMaterial.renderQueue = 3035;
        }

        dropletMaterial.SetColor("_BaseColor", paintColor);
        dropletMaterial.SetFloat("_Alpha", paintColor.a);
        dropletMaterial.SetFloat("_GlossBoost", 1.15f);
    }

    private Mesh CreateOctaDropletMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Phase05C_Instanced_OctaDroplet";
        Vector3[] v =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, -1f, 0f)
        };
        int[] t =
        {
            0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4,
            5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1
        };
        Vector3[] n = new Vector3[v.Length];
        for (int i = 0; i < n.Length; i++) n[i] = v[i].normalized;
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.normals = n;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ApplyPresentationLock(bool forceBudgetReset)
    {
        Application.targetFrameRate = 90;
        QualitySettings.vSyncCount = 0;
        Time.fixedDeltaTime = Mathf.Clamp(Time.fixedDeltaTime, 0.0125f, 0.018f);
        Time.maximumDeltaTime = 0.06f;

        if (adaptiveQualityGovernor != null)
        {
            adaptiveQualityGovernor.allowAutomaticDowngrade = false;
            adaptiveQualityGovernor.allowAutomaticRestore = false;
            adaptiveQualityGovernor.startupGraceSeconds = Mathf.Max(adaptiveQualityGovernor.startupGraceSeconds, 20f);
        }

        if (sphBudgetController != null)
        {
            sphBudgetController.targetParticleCount = 1000000;
            sphBudgetController.renderStride = Mathf.Max(sphBudgetController.renderStride, normalRenderStride);
            sphBudgetController.legacyCpuFallbackParticles = Mathf.Min(sphBudgetController.legacyCpuFallbackParticles, 700);
            sphBudgetController.legacyVisualDroplets = Mathf.Min(sphBudgetController.legacyVisualDroplets, 72);
        }

        if (gpuSphController != null)
        {
            gpuSphController.drawGpuParticles = false;
            gpuSphController.showRuntimeOverlay = false;
            gpuSphController.solverSubsteps = 1;
            gpuSphController.maxSolverDeltaTime = 0.0166667f;
            gpuSphController.gridResolutionX = Mathf.Min(gpuSphController.gridResolutionX, presentationGridX);
            gpuSphController.gridResolutionY = Mathf.Min(gpuSphController.gridResolutionY, presentationGridY);
            gpuSphController.gridResolutionZ = Mathf.Min(gpuSphController.gridResolutionZ, presentationGridZ);
            gpuSphController.maxParticlesPerCell = Mathf.Min(gpuSphController.maxParticlesPerCell, presentationCellSlots);
            gpuSphController.renderStride = Mathf.Max(gpuSphController.renderStride, gpuSphController.particleCapacity >= 1000000 ? millionRenderStride : normalRenderStride);
            gpuSphController.renderRadiusScale = Mathf.Max(gpuSphController.renderRadiusScale, 2.25f);
            gpuSphController.spillPush = Mathf.Clamp(gpuSphController.spillPush, 1.55f, 2.45f);
            gpuSphController.maxParticleVelocity = Mathf.Clamp(gpuSphController.maxParticleVelocity, 10f, 15f);
            gpuSphController.pourTiltThreshold = Mathf.Clamp(gpuSphController.pourTiltThreshold, 0.28f, 0.38f);
            gpuSphController.airborneParticleRenderScale = Mathf.Clamp(gpuSphController.airborneParticleRenderScale, 0.42f, 0.92f);

            if (!gpuSphController.Initialized && !gpuSphController.BuffersAllocated)
            {
                gpuSphController.particleCapacity = Mathf.Clamp(defaultSimulationParticles, 32768, maxPresentationParticles);
            }

            if (forceBudgetReset && gpuSphController.enableRuntime)
            {
                gpuSphController.ResetGpuFluid();
            }
        }

        if (heroVisualRenderer != null)
        {
            heroVisualRenderer.enableHeroVisuals = true;
            heroVisualRenderer.showHeroDroplets = true;
            heroVisualRenderer.dropletCount = Mathf.Clamp(heroVisualRenderer.dropletCount, 48, 80);
            heroVisualRenderer.streamLengthGain = Mathf.Clamp(heroVisualRenderer.streamLengthGain, 1.12f, 1.55f);
            heroVisualRenderer.streamStartRadius = Mathf.Clamp(heroVisualRenderer.streamStartRadius, 0.058f, 0.082f);
            heroVisualRenderer.streamEndRadius = Mathf.Clamp(heroVisualRenderer.streamEndRadius, 0.065f, 0.095f);
            heroVisualRenderer.streamSegments = Mathf.Clamp(heroVisualRenderer.streamSegments, 18, 26);
            heroVisualRenderer.streamSides = Mathf.Clamp(heroVisualRenderer.streamSides, 8, 12);
            heroVisualRenderer.visualPourTiltThreshold = Mathf.Clamp(heroVisualRenderer.visualPourTiltThreshold, 0.26f, 0.34f);
        }

        if (phase05BDirector != null)
        {
            phase05BDirector.heavyBlobImpactsPerSecond = Mathf.Max(phase05BDirector.heavyBlobImpactsPerSecond, 13f);
            phase05BDirector.satelliteDripsPerSecond = Mathf.Max(phase05BDirector.satelliteDripsPerSecond, 10f);
            phase05BDirector.fallVelocityScale = Mathf.Max(phase05BDirector.fallVelocityScale, 1.75f);
            phase05BDirector.pourTiltThreshold = Mathf.Min(phase05BDirector.pourTiltThreshold, 0.32f);
        }

        if (paintEmitter != null)
        {
            paintEmitter.ApplyPerformanceBudget(0.30f, 78f, 80, 48f);
            paintEmitter.downwardStartSpeed = Mathf.Max(paintEmitter.downwardStartSpeed, 2.6f);
            paintEmitter.randomSpread = Mathf.Clamp(paintEmitter.randomSpread, 0.035f, 0.070f);
            paintEmitter.flowNoiseAmount = Mathf.Clamp(paintEmitter.flowNoiseAmount, 0.050f, 0.110f);
        }

        if (legacyParticleSimulator != null)
        {
            legacyParticleSimulator.ApplyPerformanceBudget(950, 180, 0, false, true);
            legacyParticleSimulator.autoCullWhenOverBudget = true;
        }

        if (canvasPainter != null)
        {
            canvasPainter.fluidTextureUpdateInterval = Mathf.Max(canvasPainter.fluidTextureUpdateInterval, 0.055f);
            canvasPainter.fluidThicknessVisibility = Mathf.Clamp(canvasPainter.fluidThicknessVisibility, 0.86f, 1.25f);
            canvasPainter.fluidNormalStrength = Mathf.Clamp(canvasPainter.fluidNormalStrength, 3.8f, 5.8f);
            canvasPainter.fluidSpecularStrength = Mathf.Clamp(canvasPainter.fluidSpecularStrength, 0.42f, 0.68f);
        }
    }

    private void TrackFps()
    {
        fpsAccum += Time.unscaledDeltaTime;
        fpsWindowTimer += Time.unscaledDeltaTime;
        fpsFrames++;
        if (fpsWindowTimer >= 0.5f)
        {
            float fps = fpsFrames / Mathf.Max(0.0001f, fpsAccum);
            smoothedFps = smoothedFps <= 0.1f ? fps : Mathf.Lerp(smoothedFps, fps, 0.35f);
            fpsFrames = 0;
            fpsAccum = 0f;
            fpsWindowTimer = 0f;
        }
    }

    private void ProtectFpsIfNeeded()
    {
        if (gpuSphController == null || !gpuSphController.enableRuntime || smoothedFps <= 0.1f)
        {
            return;
        }

        if (smoothedFps < editorFpsProtectionThreshold)
        {
            lowFpsTimer += Time.unscaledDeltaTime;
        }
        else
        {
            lowFpsTimer = Mathf.Max(0f, lowFpsTimer - Time.unscaledDeltaTime * 2f);
        }

        if (!protectedBudgetApplied && lowFpsTimer > lowFpsSecondsBeforeProtect && gpuSphController.particleCapacity > maxPresentationParticles)
        {
            gpuSphController.particleCapacity = Mathf.Clamp(maxPresentationParticles, 131072, 1000000);
            gpuSphController.renderStride = Mathf.Max(gpuSphController.renderStride, millionRenderStride);
            gpuSphController.solverSubsteps = 1;
            gpuSphController.ResetGpuFluid();
            protectedBudgetApplied = true;
            Debug.LogWarning("[VR SPH Phase05C] Editor FPS protection reduced the live solver budget while keeping the perceived million-particle visual layer active. Build mode can still test full 1,000,000 particles.");
        }
    }

    private struct PourFrame
    {
        public bool pouring;
        public float flow01;
        public Vector3 source;
        public Vector3 target;
        public Vector3 controlA;
        public Vector3 controlB;
        public Vector3 side;
        public Vector3 direction;
    }

    private PourFrame BuildPourFrame()
    {
        PourFrame frame = new PourFrame();
        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity.normalized : Vector3.down;
        Vector3 gravityLocal3 = bucketTransform != null ? bucketTransform.InverseTransformDirection(gravity) : gravity;
        Vector2 gravityLocal = new Vector2(gravityLocal3.x, gravityLocal3.z);
        tilt01 = Mathf.Clamp01(gravityLocal.magnitude);
        flow01 = Mathf.Clamp01((tilt01 - visualPourTiltThreshold) / Mathf.Max(0.001f, 1f - visualPourTiltThreshold));
        flow01 = Mathf.SmoothStep(0f, 1f, flow01) * flowAmplification;
        flow01 = Mathf.Clamp01(flow01);
        frame.pouring = flow01 > 0.015f && ResolveFill01() > 0.03f;
        frame.flow01 = flow01;

        Vector2 downhill = gravityLocal.sqrMagnitude > 0.00001f ? gravityLocal.normalized : Vector2.right;
        float radius = ResolveBucketRadius() * 0.94f;
        float rimY = ResolveBucketRimY() + 0.015f;
        frame.source = bucketTransform != null ? bucketTransform.TransformPoint(new Vector3(downhill.x * radius, rimY, downhill.y * radius)) : Vector3.zero;

        Vector3 outLocal = new Vector3(downhill.x, -0.22f, downhill.y).normalized;
        Vector3 outWorld = bucketTransform != null ? bucketTransform.TransformDirection(outLocal).normalized : Vector3.down;
        Vector3 dir = Vector3.Slerp(outWorld, gravity, 0.52f).normalized;
        if (dir.y > -0.08f) dir = Vector3.Slerp(dir, gravity, 0.78f).normalized;
        frame.direction = dir;

        float planeY = ResolveSurfaceY();
        float t = Mathf.Clamp((frame.source.y - planeY) / Mathf.Max(0.08f, -dir.y), 0.18f, 3.6f);
        Vector3 target = frame.source + dir * t * 1.08f;
        target.y = planeY;
        if (canvasPainter != null)
        {
            Vector3 canvasCenter = canvasPainter.transform.position;
            canvasCenter.y = planeY;
            target = Vector3.Lerp(target, canvasCenter, 0.18f);
        }
        frame.target = target;

        Vector3 chord = frame.target - frame.source;
        Vector3 side = Vector3.Cross(chord.sqrMagnitude > 0.0001f ? chord.normalized : Vector3.down, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        frame.side = side.normalized;
        frame.controlA = Vector3.Lerp(frame.source, frame.target, 0.27f) + dir * 0.10f + frame.side * Mathf.Sin(Time.time * 3.0f) * 0.018f;
        frame.controlB = Vector3.Lerp(frame.source, frame.target, 0.68f) + gravity * 0.20f + frame.side * Mathf.Sin(Time.time * 2.4f + 1.0f) * 0.024f;
        return frame;
    }

    private int ActiveDropletCount(PourFrame frame)
    {
        return Mathf.Clamp(Mathf.RoundToInt(perceivedDropletCount * Mathf.Lerp(0.42f, 1f, frame.flow01)), 0, MaxInstances);
    }

    private void DrawDenseDropletField(PourFrame frame)
    {
        int count = ActiveDropletCount(frame);
        for (int i = 0; i < count; i++)
        {
            float h0 = Hash01(i * 19 + 3);
            float h1 = Hash01(i * 23 + 7);
            float h2 = Hash01(i * 29 + 11);
            float speed = dropletFallSpeed * Mathf.Lerp(0.72f, 1.38f, h0);
            float u = Mathf.Repeat(h1 + Time.time * speed, 1.0f);
            u = Mathf.Lerp(0.05f, 0.98f, u);

            Vector3 p = Bezier(frame.source, frame.controlA, frame.controlB, frame.target, u);
            float lateral = (h2 * 2f - 1f) * dropletCloudWidth * Mathf.Lerp(0.20f, 1.0f, u) * frame.flow01;
            float verticalMist = (Hash01(i * 31 + 17) * 2f - 1f) * dropletCloudWidth * 0.22f * Mathf.Lerp(0.1f, 1f, u);
            p += frame.side * lateral + Vector3.up * verticalMist;

            bool mist = h0 < microMistFraction;
            float scale = Mathf.Lerp(minDropletScale, maxDropletScale, Hash01(i * 37 + 5));
            if (mist) scale *= 0.52f;
            scale *= Mathf.Lerp(0.58f, 1.18f, frame.flow01);
            Vector3 stretch = mist ? new Vector3(0.65f, 1.55f, 0.65f) : new Vector3(0.85f, 1.25f, 0.85f);
            matrices[i] = Matrix4x4.TRS(p, Quaternion.identity, Vector3.Scale(Vector3.one * scale, stretch));
        }

        dropletMaterial.SetColor("_BaseColor", paintColor);
        dropletMaterial.SetFloat("_Alpha", Mathf.Lerp(0.48f, 0.86f, frame.flow01));
        int offset = 0;
        while (offset < count)
        {
            int batch = Mathf.Min(InstanceBatchSize, count - offset);
            if (offset == 0)
            {
                Graphics.DrawMeshInstanced(dropletMesh, 0, dropletMaterial, matrices, batch, null, ShadowCastingMode.Off, false, gameObject.layer);
            }
            else
            {
                Matrix4x4[] temp = new Matrix4x4[batch];
                System.Array.Copy(matrices, offset, temp, 0, batch);
                Graphics.DrawMeshInstanced(dropletMesh, 0, dropletMaterial, temp, batch, null, ShadowCastingMode.Off, false, gameObject.layer);
            }
            offset += batch;
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
        if (canvasPainter != null) return canvasPainter.transform.position.y + surfaceYOffset;
        if (gpuSphController != null && gpuSphController.surfacePlane != null) return gpuSphController.surfacePlane.position.y + surfaceYOffset;
        return surfaceYOffset;
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    private static float Hash01(int n)
    {
        float x = Mathf.Sin(n * 12.9898f + 78.233f) * 43758.5453f;
        return x - Mathf.Floor(x);
    }

    public string StatusLine => statusLine;
    public float SmoothedFps => smoothedFps;
    public bool FpsProtectionApplied => protectedBudgetApplied;
}
