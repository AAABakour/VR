using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(45)]
public class RealGpuSphController : MonoBehaviour
{
    private const string RuntimeObjectName = "RealGpuSphController_Phase05";
    private const int ThreadGroupSize = 256;
    private const int ParticleStrideBytes = 64;
    private const int GridCounterStrideBytes = 4;
    private const int GridIndexStrideBytes = 4;

    [Header("Runtime Control")]
    public bool enableRuntime = false;
    public bool autoCreateRuntimeObject = true;
    public bool showRuntimeOverlay = true;
    public bool drawGpuParticles = false;
    public bool initializeOnEnable = true;
    public bool enableRealNeighborSph = true;
    public bool enablePhase04VisualReconstruction = true;
    public bool enablePhase05ImpactQualityBridge = true;

    [Header("Keyboard Controls")]
    public KeyCode toggleRuntimeKey = KeyCode.F9;
    public KeyCode millionParticleKey = KeyCode.F11;
    public KeyCode resetSimulationKey = KeyCode.F12;

    [Header("GPU Assets")]
    public ComputeShader solverCompute;
    public Material renderMaterial;
    public Mesh renderMesh;

    [Header("Scene References")]
    public Transform bucketTransform;
    public Renderer bucketRenderer;
    public Transform surfacePlane;
    public SphFluidVolumeVisualRenderer volumeVisualRenderer;
    public SphParticleBudgetController budgetController;
    public RuntimeAdaptiveQualityGovernor adaptiveQualityGovernor;

    [Header("Particle Budget")]
    [Range(8192, 4000000)] public int particleCapacity = 262144;
    [Range(1, 256)] public int renderStride = 16;
    [Range(1, 4)] public int solverSubsteps = 2;
    [Range(0.002f, 0.033f)] public float maxSolverDeltaTime = 0.0083333f;
    [Range(0.002f, 0.04f)] public float particleRadius = 0.0095f;
    [Range(0.5f, 5.0f)] public float renderRadiusScale = 1.65f;

    [Header("Bucket Calibration")]
    [Range(0.05f, 2.0f)] public float bucketRadiusLocal = 0.32f;
    public float bucketBottomLocalY = -0.42f;
    public float bucketRimLocalY = 0.42f;
    [Range(0.05f, 0.98f)] public float initialFill01 = 0.72f;
    [Range(0.0f, 1.0f)] public float bucketWallBounce = 0.12f;

    [Header("Paint Physics")]
    public Color paintColor = new Color(0.62f, 0.012f, 0.004f, 0.96f);
    [Range(0.0f, 12.0f)] public float viscosityDrag = 4.2f;
    [Range(0.0f, 8.0f)] public float cohesionStrength = 4.8f;
    [Range(0.05f, 0.95f)] public float pourTiltThreshold = 0.46f;
    [Range(0.01f, 0.35f)] public float pourBandHeight = 0.055f;
    [Range(0.0f, 6.0f)] public float spillPush = 1.25f;
    [Range(0.0f, 5.0f)] public float sloshAccelerationGain = 0.08f;

    [Header("Phase 04 SPH Neighbor Grid")]
    [Range(16, 160)] public int gridResolutionX = 96;
    [Range(12, 128)] public int gridResolutionY = 64;
    [Range(16, 160)] public int gridResolutionZ = 96;
    [Range(2, 16)] public int maxParticlesPerCell = 8;
    [Range(1.5f, 5.0f)] public float smoothingRadiusMultiplier = 2.65f;
    [Range(1.0f, 24.0f)] public float restNeighborDensity = 7.5f;
    [Range(0.0f, 65.0f)] public float pressureStiffness = 18.0f;
    [Range(0.0f, 90.0f)] public float nearPressureStiffness = 26.0f;
    [Range(0.0f, 18.0f)] public float sphViscosityStrength = 5.5f;
    [Range(0.0f, 8.0f)] public float surfaceTension = 1.35f;
    [Range(0.0f, 1.0f)] public float boundaryDamping = 0.32f;
    [Range(1.0f, 50.0f)] public float maxParticleVelocity = 7.5f;
    [Range(0.0f, 1.0f)] public float densityColorBoost = 0.16f;


    [Header("Phase 04 Visual Reconstruction")]
    [Tooltip("Contained particles are still simulated on the GPU, but the visual mass is reconstructed by a smooth volume renderer. This avoids the noisy point-cloud look inside the bucket.")]
    public bool softenContainedParticles = true;
    [Range(0.0f, 1.0f)] public float containedParticleRenderScale = 0.0f;
    [Range(0.5f, 4.0f)] public float airborneParticleRenderScale = 0.75f;
    [Range(0.1f, 1.5f)] public float depositedParticleRenderScale = 0.05f;
    [Range(0.0f, 0.20f)] public float velocityRadiusGain = 0.010f;
    [Range(0.0f, 0.35f)] public float densityRadiusGain = 0.035f;

    [Header("Surface Collision Preview")]
    public bool enableSimpleSurfacePlane = true;
    public float surfacePlaneYOffset = 0.018f;

    [Header("Runtime Readout")]
    [SerializeField] private bool buffersAllocated;
    [SerializeField] private bool initialized;
    [SerializeField] private int visibleParticleCount;
    [SerializeField] private float estimatedParticleBufferMb;
    [SerializeField] private string runtimeStatus = "Phase 05 GPU SPH + cohesive collision impacts installed. Press F9 for preview, F11 for 1,000,000 particles.";

    private ComputeBuffer particleBuffer;
    private ComputeBuffer renderParticleBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer gridCellCountsBuffer;
    private ComputeBuffer gridParticleIndicesBuffer;

    private int kernelInitialize;
    private int kernelClearGrid;
    private int kernelBuildGrid;
    private int kernelDensityPressure;
    private int kernelIntegrateSph;
    private int kernelStep;
    private int kernelGenerateRender;
    private bool kernelsReady;
    private bool phase03KernelsReady;
    private int gridCellCount;
    private int gridSlotCount;

    private Vector3 previousBucketPosition;
    private Vector3 bucketVelocity;
    private Vector3 previousBucketVelocity;
    private bool hasPreviousBucketState;

    private Bounds drawBounds = new Bounds(Vector3.zero, Vector3.one * 12f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<RealGpuSphController>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        RealGpuSphController controller = obj.AddComponent<RealGpuSphController>();
        controller.enableRuntime = false;
    }

    private void Awake()
    {
        AutoFindReferences();
        LoadDefaultAssetsIfNeeded();
        PrepareKernels();
        CreateRenderMeshIfNeeded();
        CalibrateBucketFromRenderer(false);
    }

    private void Start()
    {
        AutoFindReferences();
        LoadDefaultAssetsIfNeeded();
        CalibrateBucketFromRenderer(false);
        ApplyBudgetFromController(false);

        if (enableRuntime && initializeOnEnable)
        {
            AllocateAndInitialize();
        }
    }

    private void Update()
    {
        HandleKeyboardShortcuts();

        if (!enableRuntime)
        {
            return;
        }

        if (!buffersAllocated)
        {
            AllocateAndInitialize();
        }

        if (!buffersAllocated || solverCompute == null || !kernelsReady)
        {
            runtimeStatus = "Phase 05 GPU SPH waiting for compute shader/buffers.";
            return;
        }

        UpdateBucketVelocity();
        DispatchSimulation(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (enableRuntime && drawGpuParticles && buffersAllocated && renderMaterial != null && renderMesh != null && argsBuffer != null)
        {
            UpdateRenderResources();
            Graphics.DrawMeshInstancedIndirect(
                renderMesh,
                0,
                renderMaterial,
                drawBounds,
                argsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                true,
                gameObject.layer
            );
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void OnGUI()
    {
        if (!showRuntimeOverlay || !enableRuntime)
        {
            return;
        }

        GUI.Box(new Rect(12f, 12f, 470f, 96f), "");
        GUI.Label(new Rect(24f, 20f, 440f, 22f), "Real GPU SPH Phase 05 Collision + Visual Core");
        GUI.Label(new Rect(24f, 44f, 440f, 22f), StatusLine);
        GUI.Label(new Rect(24f, 68f, 440f, 22f), "Keys: F9 toggle | F11 million particles | F12 reset");
    }

    private void HandleKeyboardShortcuts()
    {
        if (WasShortcutPressed(toggleRuntimeKey))
        {
            SetRuntimeEnabled(!enableRuntime, false);
        }

        if (WasShortcutPressed(millionParticleKey))
        {
            SetMillionParticleMode();
        }

        if (WasShortcutPressed(resetSimulationKey))
        {
            ResetGpuFluid();
        }
    }

    private bool WasShortcutPressed(KeyCode legacyKey)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (legacyKey)
            {
                case KeyCode.F1: return keyboard.f1Key.wasPressedThisFrame;
                case KeyCode.F2: return keyboard.f2Key.wasPressedThisFrame;
                case KeyCode.F3: return keyboard.f3Key.wasPressedThisFrame;
                case KeyCode.F4: return keyboard.f4Key.wasPressedThisFrame;
                case KeyCode.F5: return keyboard.f5Key.wasPressedThisFrame;
                case KeyCode.F6: return keyboard.f6Key.wasPressedThisFrame;
                case KeyCode.F7: return keyboard.f7Key.wasPressedThisFrame;
                case KeyCode.F8: return keyboard.f8Key.wasPressedThisFrame;
                case KeyCode.F9: return keyboard.f9Key.wasPressedThisFrame;
                case KeyCode.F10: return keyboard.f10Key.wasPressedThisFrame;
                case KeyCode.F11: return keyboard.f11Key.wasPressedThisFrame;
                case KeyCode.F12: return keyboard.f12Key.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.R: return keyboard.rKey.wasPressedThisFrame;
                case KeyCode.G: return keyboard.gKey.wasPressedThisFrame;
                default: break;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(legacyKey);
#else
        return false;
#endif
    }

    private void AutoFindReferences()
    {
        if (budgetController == null) budgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
        if (adaptiveQualityGovernor == null) adaptiveQualityGovernor = Object.FindFirstObjectByType<RuntimeAdaptiveQualityGovernor>();
        if (volumeVisualRenderer == null) volumeVisualRenderer = Object.FindFirstObjectByType<SphFluidVolumeVisualRenderer>();

        if (bucketTransform == null)
        {
            GameObject bucket = GameObject.Find("Bucket");
            if (bucket != null) bucketTransform = bucket.transform;
        }

        if (bucketTransform == null)
        {
            BucketInteriorLiquidSystemV2 liquid = Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
            if (liquid != null) bucketTransform = liquid.transform;
        }

        if (bucketRenderer == null && bucketTransform != null)
        {
            bucketRenderer = bucketTransform.GetComponentInChildren<Renderer>();
        }

        if (surfacePlane == null)
        {
            CanvasPainter painter = Object.FindFirstObjectByType<CanvasPainter>();
            if (painter != null) surfacePlane = painter.transform;
        }

        if (volumeVisualRenderer != null)
        {
            volumeVisualRenderer.gpuSphController = this;
            if (bucketTransform != null) volumeVisualRenderer.bucketTransform = bucketTransform;
            if (bucketRenderer != null) volumeVisualRenderer.bucketRenderer = bucketRenderer;
        }
    }

    private void LoadDefaultAssetsIfNeeded()
    {
        if (solverCompute == null)
        {
            solverCompute = Resources.Load<ComputeShader>("RealBucketSPH_Phase02");
        }

        if (renderMaterial == null)
        {
            Shader shader = Shader.Find("VR/Paint/GPU SPH Indirect URP");
            if (shader != null)
            {
                renderMaterial = new Material(shader);
                renderMaterial.name = "Runtime_GpuSphIndirectPaint";
                renderMaterial.SetColor("_BaseColor", paintColor);
                renderMaterial.SetFloat("_Alpha", paintColor.a);
            }
        }
    }

    private void PrepareKernels()
    {
        kernelsReady = false;
        phase03KernelsReady = false;
        if (solverCompute == null)
        {
            return;
        }

        try
        {
            kernelInitialize = solverCompute.FindKernel("CSInitializeParticles");
            kernelGenerateRender = solverCompute.FindKernel("CSGenerateRenderParticles");

            try
            {
                kernelClearGrid = solverCompute.FindKernel("CSClearGrid");
                kernelBuildGrid = solverCompute.FindKernel("CSBuildGrid");
                kernelDensityPressure = solverCompute.FindKernel("CSComputeDensityPressure");
                kernelIntegrateSph = solverCompute.FindKernel("CSIntegrateSph");
                phase03KernelsReady = true;
            }
            catch
            {
                phase03KernelsReady = false;
            }

            // Keep the old Phase 02 integration kernel as a fallback only.
            try
            {
                kernelStep = solverCompute.FindKernel("CSStepBucketFluid");
            }
            catch
            {
                kernelStep = -1;
            }

            kernelsReady = phase03KernelsReady || kernelStep >= 0;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[VR SPH Phase04] Compute kernels are not ready: " + ex.Message);
            runtimeStatus = "Compute kernels not ready.";
        }
    }

    private void CreateRenderMeshIfNeeded()
    {
        if (renderMesh != null)
        {
            return;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Runtime_OctaPaintParticleMesh";

        Vector3[] vertices =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, -1f, 0f)
        };

        int[] triangles =
        {
            0, 2, 1,
            0, 3, 2,
            0, 4, 3,
            0, 1, 4,
            5, 1, 2,
            5, 2, 3,
            5, 3, 4,
            5, 4, 1
        };

        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = vertices[i].normalized;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        renderMesh = mesh;
    }

    private void CalibrateBucketFromRenderer(bool logResult)
    {
        if (bucketRenderer == null || bucketTransform == null)
        {
            return;
        }

        Bounds bounds = bucketRenderer.bounds;
        Vector3 localMin = bucketTransform.InverseTransformPoint(bounds.min);
        Vector3 localMax = bucketTransform.InverseTransformPoint(bounds.max);

        float yMin = Mathf.Min(localMin.y, localMax.y);
        float yMax = Mathf.Max(localMin.y, localMax.y);
        float worldRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.72f;
        float scaleX = Mathf.Max(0.001f, Mathf.Abs(bucketTransform.lossyScale.x));
        float scaleZ = Mathf.Max(0.001f, Mathf.Abs(bucketTransform.lossyScale.z));
        float localScale = Mathf.Max(scaleX, scaleZ);

        bucketRadiusLocal = Mathf.Clamp(worldRadius / localScale, 0.08f, 2.0f);
        bucketBottomLocalY = yMin + 0.04f;
        bucketRimLocalY = yMax - 0.04f;

        if (bucketRimLocalY <= bucketBottomLocalY + 0.08f)
        {
            bucketBottomLocalY = -0.42f;
            bucketRimLocalY = 0.42f;
        }

        if (logResult)
        {
            Debug.Log("[VR SPH Phase04] Bucket calibrated | radius local " + bucketRadiusLocal.ToString("0.000") +
                      " | bottom " + bucketBottomLocalY.ToString("0.000") +
                      " | rim " + bucketRimLocalY.ToString("0.000"));
        }
    }

    private void ApplyBudgetFromController(bool forceMillion)
    {
        if (budgetController != null)
        {
            int target = forceMillion ? Mathf.Max(1000000, budgetController.targetParticleCount) : Mathf.Min(262144, budgetController.targetParticleCount);
            particleCapacity = Mathf.Clamp(target, 8192, 4000000);
            renderStride = Mathf.Clamp(budgetController.renderStride, 1, 256);
        }

        if (forceMillion)
        {
            particleCapacity = Mathf.Max(1000000, particleCapacity);
            renderStride = Mathf.Max(8, renderStride);
        }
    }

    public void SetRuntimeEnabled(bool enabled, bool forceMillion)
    {
        AutoFindReferences();
        LoadDefaultAssetsIfNeeded();
        PrepareKernels();
        CreateRenderMeshIfNeeded();
        CalibrateBucketFromRenderer(false);
        ApplyBudgetFromController(forceMillion);

        enableRuntime = enabled;

        if (enableRuntime)
        {
            AllocateAndInitialize();
        }
        else
        {
            runtimeStatus = "Phase 05 GPU SPH paused.";
        }
    }

    public void SetMillionParticleMode()
    {
        SetRuntimeEnabled(true, true);
    }

    public void ResetGpuFluid()
    {
        if (!enableRuntime)
        {
            runtimeStatus = "GPU fluid reset requested while runtime is disabled.";
            return;
        }

        AllocateAndInitialize();
    }

    public void AllocateAndInitialize()
    {
        AutoFindReferences();
        LoadDefaultAssetsIfNeeded();
        PrepareKernels();
        CreateRenderMeshIfNeeded();
        CalibrateBucketFromRenderer(true);

        if (solverCompute == null || !kernelsReady)
        {
            runtimeStatus = "Missing RealBucketSPH compute shader with Phase04 parameters.";
            Debug.LogWarning("[VR SPH Phase05] " + runtimeStatus);
            return;
        }

        ReleaseBuffers();

        particleCapacity = Mathf.Clamp(particleCapacity, 8192, 4000000);
        renderStride = Mathf.Clamp(renderStride, 1, 256);
        visibleParticleCount = Mathf.Max(1, Mathf.CeilToInt(particleCapacity / (float)renderStride));
        ClampGridSettings();

        particleBuffer = new ComputeBuffer(particleCapacity, ParticleStrideBytes, ComputeBufferType.Structured);
        renderParticleBuffer = new ComputeBuffer(visibleParticleCount, sizeof(float) * 4, ComputeBufferType.Structured);
        argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

        if (phase03KernelsReady)
        {
            gridCellCountsBuffer = new ComputeBuffer(gridCellCount, GridCounterStrideBytes, ComputeBufferType.Structured);
            gridParticleIndicesBuffer = new ComputeBuffer(gridSlotCount, GridIndexStrideBytes, ComputeBufferType.Structured);
        }

        uint indexCount = renderMesh != null ? renderMesh.GetIndexCount(0) : 0u;
        uint[] args = { indexCount, (uint)visibleParticleCount, 0u, 0u, 0u };
        argsBuffer.SetData(args);

        SetCommonComputeParameters();
        BindComputeBuffersForAllKernels();

        int groups = GroupsFor(particleCapacity);
        solverCompute.Dispatch(kernelInitialize, groups, 1, 1);

        buffersAllocated = true;
        initialized = true;
        float gridMb = phase03KernelsReady ? (gridCellCount * GridCounterStrideBytes + gridSlotCount * GridIndexStrideBytes) / (1024f * 1024f) : 0f;
        estimatedParticleBufferMb = (particleCapacity * ParticleStrideBytes + visibleParticleCount * 16f + 20f) / (1024f * 1024f) + gridMb;
        runtimeStatus = "Initialized " + particleCapacity.ToString("N0") + " GPU particles | visible " + visibleParticleCount.ToString("N0") + " | grid " + gridResolutionX + "x" + gridResolutionY + "x" + gridResolutionZ + " x" + maxParticlesPerCell + " | buffer ~" + estimatedParticleBufferMb.ToString("0.0") + " MB";
        Debug.Log("[VR SPH Phase05] " + runtimeStatus);
    }

    public void ReleaseBuffers()
    {
        if (particleBuffer != null)
        {
            particleBuffer.Release();
            particleBuffer = null;
        }

        if (renderParticleBuffer != null)
        {
            renderParticleBuffer.Release();
            renderParticleBuffer = null;
        }

        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }

        if (gridCellCountsBuffer != null)
        {
            gridCellCountsBuffer.Release();
            gridCellCountsBuffer = null;
        }

        if (gridParticleIndicesBuffer != null)
        {
            gridParticleIndicesBuffer.Release();
            gridParticleIndicesBuffer = null;
        }

        buffersAllocated = false;
        initialized = false;
    }

    private void DispatchSimulation(float frameDeltaTime)
    {
        float dt = Mathf.Min(frameDeltaTime, maxSolverDeltaTime);
        int substeps = Mathf.Clamp(solverSubsteps, 1, 4);
        float subDt = dt / substeps;

        SetCommonComputeParameters();
        BindComputeBuffersForAllKernels();

        int particleGroups = GroupsFor(particleCapacity);

        for (int i = 0; i < substeps; i++)
        {
            solverCompute.SetFloat("_DeltaTime", subDt);

            if (enableRealNeighborSph && phase03KernelsReady && gridCellCountsBuffer != null && gridParticleIndicesBuffer != null)
            {
                solverCompute.Dispatch(kernelClearGrid, GroupsFor(gridCellCount), 1, 1);
                solverCompute.Dispatch(kernelBuildGrid, particleGroups, 1, 1);
                solverCompute.Dispatch(kernelDensityPressure, particleGroups, 1, 1);
                solverCompute.Dispatch(kernelIntegrateSph, particleGroups, 1, 1);
            }
            else if (kernelStep >= 0)
            {
                solverCompute.Dispatch(kernelStep, particleGroups, 1, 1);
            }
        }

        solverCompute.SetInt("_VisibleParticleCount", visibleParticleCount);
        solverCompute.SetInt("_RenderStride", renderStride);
        solverCompute.SetFloat("_RenderRadiusScale", renderRadiusScale);
        solverCompute.SetBuffer(kernelGenerateRender, "_Particles", particleBuffer);
        solverCompute.SetBuffer(kernelGenerateRender, "_RenderParticles", renderParticleBuffer);
        solverCompute.Dispatch(kernelGenerateRender, GroupsFor(visibleParticleCount), 1, 1);

        string mode = enableRealNeighborSph && phase03KernelsReady ? "Phase05 SPH" : "Phase02 fallback";
        runtimeStatus = mode + " " + particleCapacity.ToString("N0") + " particles | visible " + visibleParticleCount.ToString("N0") + " | grid " + gridResolutionX + "x" + gridResolutionY + "x" + gridResolutionZ + " | stride " + renderStride + " | substeps " + substeps + (enablePhase04VisualReconstruction ? " | volume visual" : "") + (enablePhase05ImpactQualityBridge ? " | phase05 impacts" : "");
    }

    private void SetCommonComputeParameters()
    {
        Matrix4x4 bucketToWorld = bucketTransform != null ? bucketTransform.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 worldToBucket = bucketTransform != null ? bucketTransform.worldToLocalMatrix : Matrix4x4.identity;
        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity : new Vector3(0f, -9.81f, 0f);
        Vector3 gravityLocal = worldToBucket.MultiplyVector(gravity);
        Vector3 sloshAcceleration = EstimateSloshAcceleration();
        ClampGridSettings();

        solverCompute.SetInt("_ParticleCount", particleCapacity);
        solverCompute.SetInt("_VisibleParticleCount", visibleParticleCount);
        solverCompute.SetInt("_RenderStride", renderStride);
        solverCompute.SetInt("_CellCount", gridCellCount);
        solverCompute.SetInt("_GridMaxParticlesPerCell", maxParticlesPerCell);
        solverCompute.SetInts("_GridResolution", gridResolutionX, gridResolutionY, gridResolutionZ);
        solverCompute.SetFloat("_DeltaTime", Mathf.Min(Time.deltaTime, maxSolverDeltaTime));
        solverCompute.SetFloat("_ParticleRadius", particleRadius);
        solverCompute.SetFloat("_RenderRadiusScale", renderRadiusScale);
        solverCompute.SetFloat("_BucketRadius", bucketRadiusLocal);
        solverCompute.SetFloat("_BucketBottomY", bucketBottomLocalY);
        solverCompute.SetFloat("_BucketRimY", bucketRimLocalY);
        solverCompute.SetFloat("_BucketFill01", initialFill01);
        solverCompute.SetFloat("_BucketWallBounce", bucketWallBounce);
        solverCompute.SetFloat("_ViscosityDrag", viscosityDrag);
        solverCompute.SetFloat("_CohesionStrength", cohesionStrength);
        solverCompute.SetFloat("_SurfaceY", ResolveSurfaceY());
        solverCompute.SetFloat("_EnableSurfacePlane", enableSimpleSurfacePlane ? 1f : 0f);
        solverCompute.SetFloat("_PourTiltThreshold", pourTiltThreshold);
        solverCompute.SetFloat("_PourBandHeight", pourBandHeight);
        solverCompute.SetFloat("_SpillPush", spillPush);
        solverCompute.SetFloat("_TimeSeconds", Time.time);
        solverCompute.SetFloat("_SmoothingRadius", Mathf.Max(particleRadius * 2.1f, particleRadius * smoothingRadiusMultiplier));
        solverCompute.SetFloat("_RestDensity", restNeighborDensity);
        solverCompute.SetFloat("_PressureStiffness", pressureStiffness);
        solverCompute.SetFloat("_NearPressureStiffness", nearPressureStiffness);
        solverCompute.SetFloat("_ViscosityStrength", sphViscosityStrength);
        solverCompute.SetFloat("_SurfaceTension", surfaceTension);
        solverCompute.SetFloat("_BoundaryDamping", boundaryDamping);
        solverCompute.SetFloat("_MaxVelocity", maxParticleVelocity);
        solverCompute.SetFloat("_DensityToColorBoost", densityColorBoost);
        solverCompute.SetFloat("_ContainedRenderScale", softenContainedParticles ? containedParticleRenderScale : 1.0f);
        solverCompute.SetFloat("_AirborneRenderScale", airborneParticleRenderScale);
        solverCompute.SetFloat("_DepositedRenderScale", depositedParticleRenderScale);
        solverCompute.SetFloat("_VelocityRadiusGain", velocityRadiusGain);
        solverCompute.SetFloat("_DensityRadiusGain", densityRadiusGain);
        solverCompute.SetVector("_Gravity", gravity);
        solverCompute.SetVector("_GravityLocal", gravityLocal);
        solverCompute.SetVector("_BucketVelocity", bucketVelocity);
        solverCompute.SetVector("_SloshAcceleration", sloshAcceleration);
        solverCompute.SetVector("_GridOriginLocal", ResolveGridOriginLocal());
        solverCompute.SetVector("_GridSizeLocal", ResolveGridSizeLocal());
        solverCompute.SetVector("_PaintColor", paintColor);
        solverCompute.SetMatrix("_BucketToWorld", bucketToWorld);
        solverCompute.SetMatrix("_WorldToBucket", worldToBucket);
    }

private void ClampGridSettings()
    {
        gridResolutionX = Mathf.Clamp(gridResolutionX, 16, 160);
        gridResolutionY = Mathf.Clamp(gridResolutionY, 12, 128);
        gridResolutionZ = Mathf.Clamp(gridResolutionZ, 16, 160);
        maxParticlesPerCell = Mathf.Clamp(maxParticlesPerCell, 2, 16);
        gridCellCount = Mathf.Max(1, gridResolutionX * gridResolutionY * gridResolutionZ);
        gridSlotCount = Mathf.Max(1, gridCellCount * maxParticlesPerCell);
    }

    private Vector3 ResolveGridOriginLocal()
    {
        float horizontalExtent = Mathf.Max(bucketRadiusLocal * 1.45f, bucketRadiusLocal + particleRadius * 8f);
        float bottom = bucketBottomLocalY - particleRadius * 6f;
        return new Vector3(-horizontalExtent, bottom, -horizontalExtent);
    }

    private Vector3 ResolveGridSizeLocal()
    {
        float horizontalExtent = Mathf.Max(bucketRadiusLocal * 1.45f, bucketRadiusLocal + particleRadius * 8f);
        float height = Mathf.Max(0.25f, bucketRimLocalY - bucketBottomLocalY + particleRadius * 16f);
        // Extra height lets the grid handle the rim band and the first part of the pour stream.
        return new Vector3(horizontalExtent * 2f, height * 1.65f, horizontalExtent * 2f);
    }

    private void BindComputeBuffersForAllKernels()
    {
        if (solverCompute == null || particleBuffer == null)
        {
            return;
        }

        solverCompute.SetBuffer(kernelInitialize, "_Particles", particleBuffer);

        if (phase03KernelsReady && gridCellCountsBuffer != null && gridParticleIndicesBuffer != null)
        {
            solverCompute.SetBuffer(kernelClearGrid, "_GridCellCounts", gridCellCountsBuffer);
            solverCompute.SetBuffer(kernelBuildGrid, "_Particles", particleBuffer);
            solverCompute.SetBuffer(kernelBuildGrid, "_GridCellCounts", gridCellCountsBuffer);
            solverCompute.SetBuffer(kernelBuildGrid, "_GridParticleIndices", gridParticleIndicesBuffer);

            solverCompute.SetBuffer(kernelDensityPressure, "_Particles", particleBuffer);
            solverCompute.SetBuffer(kernelDensityPressure, "_GridCellCounts", gridCellCountsBuffer);
            solverCompute.SetBuffer(kernelDensityPressure, "_GridParticleIndices", gridParticleIndicesBuffer);

            solverCompute.SetBuffer(kernelIntegrateSph, "_Particles", particleBuffer);
            solverCompute.SetBuffer(kernelIntegrateSph, "_GridCellCounts", gridCellCountsBuffer);
            solverCompute.SetBuffer(kernelIntegrateSph, "_GridParticleIndices", gridParticleIndicesBuffer);
        }

        if (kernelStep >= 0)
        {
            solverCompute.SetBuffer(kernelStep, "_Particles", particleBuffer);
            if (gridCellCountsBuffer != null) solverCompute.SetBuffer(kernelStep, "_GridCellCounts", gridCellCountsBuffer);
            if (gridParticleIndicesBuffer != null) solverCompute.SetBuffer(kernelStep, "_GridParticleIndices", gridParticleIndicesBuffer);
        }
    }

    public string SolverModeLine
    {
        get
        {
            string mode = enableRealNeighborSph && phase03KernelsReady ? "Phase04 uniform-grid SPH + volume visuals" : "Phase02 fallback integration";
            return mode + " | grid " + gridResolutionX + "x" + gridResolutionY + "x" + gridResolutionZ + " | slots " + maxParticlesPerCell + " | cells " + gridCellCount.ToString("N0");
        }
    }

    public int GridCellCount => gridCellCount;
    public int GridSlotCount => gridSlotCount;
    public bool Phase03KernelsReady => phase03KernelsReady;
    public bool Phase04KernelsReady => phase03KernelsReady;

        private float ResolveSurfaceY()
    {
        if (surfacePlane != null)
        {
            return surfacePlane.position.y + surfacePlaneYOffset;
        }

        return 0f + surfacePlaneYOffset;
    }

    private void UpdateRenderResources()
    {
        if (renderMaterial != null && renderParticleBuffer != null)
        {
            renderMaterial.SetBuffer("_RenderParticles", renderParticleBuffer);
            renderMaterial.SetColor("_BaseColor", paintColor);
            renderMaterial.SetFloat("_Alpha", paintColor.a);
            renderMaterial.SetFloat("_ContainedSoftness", softenContainedParticles ? 1f : 0f);
            renderMaterial.SetFloat("_DensityBoost", densityColorBoost);
        }

        Vector3 center = bucketTransform != null ? bucketTransform.position : transform.position;
        float size = Mathf.Max(4f, bucketRadiusLocal * 8f + 3f);
        drawBounds = new Bounds(center + Vector3.down * 0.6f, new Vector3(size, size * 1.7f, size));
    }

    private void UpdateBucketVelocity()
    {
        if (bucketTransform == null)
        {
            bucketVelocity = Vector3.zero;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 currentPosition = bucketTransform.position;

        if (!hasPreviousBucketState)
        {
            previousBucketPosition = currentPosition;
            previousBucketVelocity = Vector3.zero;
            bucketVelocity = Vector3.zero;
            hasPreviousBucketState = true;
            return;
        }

        bucketVelocity = (currentPosition - previousBucketPosition) / dt;
        previousBucketPosition = currentPosition;
    }

    private Vector3 EstimateSloshAcceleration()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 acceleration = (bucketVelocity - previousBucketVelocity) / dt;
        previousBucketVelocity = bucketVelocity;
        return Vector3.ClampMagnitude(-acceleration * sloshAccelerationGain, 18f);
    }

    private int GroupsFor(int count)
    {
        return Mathf.Max(1, Mathf.CeilToInt(count / (float)ThreadGroupSize));
    }

    public string Phase04VisualLine
    {
        get
        {
            if (!enablePhase04VisualReconstruction) return "Phase04 volume visuals disabled";
            if (volumeVisualRenderer != null) return volumeVisualRenderer.VisualQualityLine;
            return "Phase04 volume visual renderer will auto-create at runtime";
        }
    }

    public string StatusLine => runtimeStatus;
    public bool BuffersAllocated => buffersAllocated;
    public bool Initialized => initialized;
    public int VisibleParticleCount => visibleParticleCount;
    public float EstimatedParticleBufferMb => estimatedParticleBufferMb;
}
