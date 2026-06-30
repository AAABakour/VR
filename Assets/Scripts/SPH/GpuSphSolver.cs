using UnityEngine;
using UnityEngine.Serialization;

public enum GpuSphSimulationDomain
{
    FluidBox = 0,
    BucketCylinder = 1,
    OpenWorldWithBounds = 2
}

[DefaultExecutionOrder(80)]
public class GpuSphSolver : MonoBehaviour
{
    public ComputeShader sphComputeShader;
    [FormerlySerializedAs("settings")]
    public GpuSphSettings settingsTemplate;
    public FluidBoxController fluidBox;
    public GpuSphSimulationDomain simulationDomain = GpuSphSimulationDomain.FluidBox;
    public Transform openWorldSimulationRoot;
    public BucketSphCollisionProvider bucketCollisionProvider;
    public GpuSphDebugStats debugStats;
    public bool initializeOnStart = true;
    public bool simulate = true;
    public bool applySettingsBoundsToBox = true;
    [Min(0.05f)]
    public float overflowReadbackInterval = 0.25f;

    private ComputeBuffer particleBuffer;
    private ComputeBuffer forceBuffer;
    private ComputeBuffer cellCountsBuffer;
    private ComputeBuffer cellParticlesBuffer;
    private ComputeBuffer overflowCountersBuffer;
    private ComputeBuffer lifecycleCountersBuffer;

    private GpuSphSettings runtimeSettings;
    private int initializeKernel;
    private int clearGridKernel;
    private int buildGridKernel;
    private int densityPressureKernel;
    private int forcesKernel;
    private int integrateKernel;
    private int collisionKernel;
    private int initializeBucketVolumeKernel;
    private int initializeNozzleEmissionKernel;
    private int emitFromNozzleKernel;
    private int bucketCollisionKernel;

    private int particleCount;
    private int renderStride;
    private int gridCellCount;
    private int maxParticlesPerCell;
    private Vector3Int gridDimensions;
    private Vector3 initGridDimensions;
    private float cellSize;
    private float initialSpacing;
    private float estimatedParticleBufferMemoryMb;
    private float estimatedGridMemoryMb;
    private float estimatedTotalGpuMemoryMb;
    private int particleDispatchGroupCount;
    private int gridDispatchGroupCount;
    private int gridOverflowCount;
    private int activeParticleCount;
    private int inactiveParticleCount;
    private int totalEmittedParticleCount;
    private float lastEmitterLifetime = 3f;
    private int emissionWriteIndex;
    private readonly int[] overflowReadbackScratch = new int[1];
    private readonly int[] lifecycleReadbackScratch = new int[3];
    private float overflowReadbackTimer;
    private bool initialized;
    private bool kernelsReady;
    private bool warnedMissingConfiguration;

    public ComputeBuffer ParticleBuffer
    {
        get { return particleBuffer; }
    }

    public GpuSphSettings RuntimeSettings
    {
        get { return runtimeSettings; }
    }

    public int ParticleCount
    {
        get { return particleCount; }
    }

    public int AllocatedParticleCount
    {
        get { return particleCount; }
    }

    public int RequestedParticleCount
    {
        get { return ActiveSettings != null ? ActiveSettings.particleCount : 0; }
    }

    public int GpuBufferParticleCapacity
    {
        get { return particleBuffer != null ? particleCount : 0; }
    }

    public int ParticleStrideBytes
    {
        get { return GpuSphBufferUtility.ParticleStrideBytes; }
    }

    public int RenderStride
    {
        get { return renderStride; }
    }

    public int RuntimeRenderStride
    {
        get { return renderStride; }
    }

    public int RenderedParticleCount
    {
        get { return Mathf.CeilToInt(particleCount / (float)Mathf.Max(1, renderStride)); }
    }

    public int AllocatedRenderedCapacity
    {
        get { return RenderedParticleCount; }
    }

    public int ExpectedRenderedParticleCount
    {
        get { return RenderedParticleCount; }
    }

    public float EstimatedGpuMemoryMb
    {
        get { return estimatedTotalGpuMemoryMb; }
    }

    public float EstimatedParticleBufferMemoryMb
    {
        get { return estimatedParticleBufferMemoryMb; }
    }

    public float EstimatedGridMemoryMb
    {
        get { return estimatedGridMemoryMb; }
    }

    public float EstimatedTotalGpuMemoryMb
    {
        get { return estimatedTotalGpuMemoryMb; }
    }

    public string ActiveModeLabel
    {
        get { return ActiveSettings != null ? ActiveSettings.modeLabel : "Unconfigured"; }
    }

    public string RuntimeProfileLabel
    {
        get { return ActiveModeLabel; }
    }

    public bool IsProfessorBenchmarkActive
    {
        get
        {
            return ActiveSettings != null &&
                ActiveSettings.modeLabel == SimulationMode.ProfessorBenchmark.ToString() &&
                particleCount >= 1000000;
        }
    }

    public Vector3Int GridDimensions
    {
        get { return gridDimensions; }
    }

    public Vector3 BoundsSize
    {
        get { return ActiveSettings != null ? ActiveSettings.boundsSize : Vector3.zero; }
    }

    public Vector3 SimulationBoundsSize
    {
        get
        {
            GpuSphSettings settings = ActiveSettings;
            return settings != null ? ResolveBoundsSize(settings) : Vector3.zero;
        }
    }

    public Matrix4x4 SimulationLocalToWorldMatrix
    {
        get { return GetSimulationLocalToWorldMatrix(); }
    }

    public Vector3 SimulationWorldCenter
    {
        get { return SimulationLocalToWorldMatrix.MultiplyPoint3x4(Vector3.zero); }
    }

    public string SimulationDomainLabel
    {
        get { return simulationDomain.ToString(); }
    }

    public Matrix4x4 GetSimulationLocalToWorldMatrix()
    {
        if (simulationDomain == GpuSphSimulationDomain.FluidBox && fluidBox != null)
        {
            return fluidBox.LocalToWorldMatrix;
        }

        if (simulationDomain == GpuSphSimulationDomain.BucketCylinder && bucketCollisionProvider != null)
        {
            return bucketCollisionProvider.LocalToWorldMatrix;
        }

        Transform root = openWorldSimulationRoot != null ? openWorldSimulationRoot : transform;
        return root.localToWorldMatrix;
    }

    public Vector3 WorldToSimulationPosition(Vector3 worldPosition)
    {
        return GetSimulationLocalToWorldMatrix().inverse.MultiplyPoint3x4(worldPosition);
    }

    public Vector3 WorldToSimulationDirection(Vector3 worldDirection)
    {
        return GetSimulationLocalToWorldMatrix().inverse.MultiplyVector(worldDirection);
    }

    public Vector3 WorldToSimulationVelocity(Vector3 worldVelocity)
    {
        return WorldToSimulationDirection(worldVelocity);
    }

    public int Substeps
    {
        get { return ActiveSettings != null ? ActiveSettings.substeps : 0; }
    }

    public float SmoothingLength
    {
        get { return ActiveSettings != null ? ActiveSettings.smoothingLength : 0f; }
    }

    public float RestDensity
    {
        get { return ActiveSettings != null ? ActiveSettings.restDensity : 0f; }
    }

    public float Viscosity
    {
        get { return ActiveSettings != null ? ActiveSettings.viscosity : 0f; }
    }

    public float Timestep
    {
        get { return ActiveSettings != null ? ActiveSettings.timestep : 0f; }
    }

    public int MaxParticlesPerCell
    {
        get { return maxParticlesPerCell; }
    }

    public int GridOverflowCount
    {
        get { return gridOverflowCount; }
    }

    public int ActiveParticleCount
    {
        get { return activeParticleCount; }
    }

    public int InactiveParticleCount
    {
        get { return inactiveParticleCount; }
    }

    public int TotalEmittedParticleCount
    {
        get { return totalEmittedParticleCount; }
    }

    public float LastEmitterLifetime
    {
        get { return lastEmitterLifetime; }
    }

    public string SimulationFrameLabel
    {
        get
        {
            switch (simulationDomain)
            {
                case GpuSphSimulationDomain.BucketCylinder:
                    return "Bucket local";
                case GpuSphSimulationDomain.OpenWorldWithBounds:
                    return "Open world/root";
                case GpuSphSimulationDomain.FluidBox:
                    return "Fluid box";
                default:
                    return "Unknown";
            }
        }
    }

    public int ParticleDispatchGroupCount
    {
        get { return particleDispatchGroupCount; }
    }

    public int GridDispatchGroupCount
    {
        get { return gridDispatchGroupCount; }
    }

    public bool IsInitialized
    {
        get { return initialized; }
    }

    public bool BuffersValid
    {
        get
        {
            return particleBuffer != null &&
                forceBuffer != null &&
                cellCountsBuffer != null &&
                cellParticlesBuffer != null &&
                overflowCountersBuffer != null &&
                lifecycleCountersBuffer != null;
        }
    }

    private GpuSphSettings ActiveSettings
    {
        get { return runtimeSettings != null ? runtimeSettings : settingsTemplate; }
    }

    private void Start()
    {
        if (initializeOnStart)
        {
            Initialize();
        }
    }

    private void Update()
    {
        if (!simulate || !initialized)
        {
            return;
        }

        StepSimulation();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    public void Initialize()
    {
        if (settingsTemplate == null || sphComputeShader == null)
        {
            WarnOnce("[GpuSphSolver] Missing settings template or compute shader.");
            return;
        }

        EnsureRuntimeSettings(false);
        runtimeSettings.ClampValues();
        particleCount = runtimeSettings.ClampedParticleCount;
        renderStride = runtimeSettings.ClampedRenderStride;
        maxParticlesPerCell = Mathf.Max(1, runtimeSettings.maxParticlesPerCell);

        if (fluidBox != null && applySettingsBoundsToBox)
        {
            fluidBox.boundsSize = runtimeSettings.boundsSize;
        }

        if (!CacheKernels())
        {
            return;
        }

        ConfigureGrid();
        ReleaseBuffers();
        AllocateBuffers();
        SetCommonShaderParameters();

        sphComputeShader.SetVector("_InitGridDims", new Vector4(initGridDimensions.x, initGridDimensions.y, initGridDimensions.z, 0f));
        DispatchInitialState();
        SeedLifecycleCountersAfterInitialize();

        initialized = true;
        UpdateDebugStats();
    }

    public void ResetSimulation()
    {
        Initialize();
    }

    public void ApplySettings(GpuSphSettings newSettings)
    {
        settingsTemplate = newSettings;
        EnsureRuntimeSettings(true);
        Initialize();
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        if (settingsTemplate == null || profile == null)
        {
            return;
        }

        EnsureRuntimeSettings(false);
        runtimeSettings.ApplySimulationProfile(profile);
        Initialize();
    }

    public void SetSimulationDomain(GpuSphSimulationDomain domain)
    {
        simulationDomain = domain;
        if (initialized)
        {
            Initialize();
        }
    }

    public void SetBucketCollisionProvider(BucketSphCollisionProvider provider)
    {
        bucketCollisionProvider = provider;
        if (initialized)
        {
            SetCommonShaderParameters();
        }
    }

    public void InitializeForNozzleEmission()
    {
        if (initialized && kernelsReady)
        {
            SetCommonShaderParameters();
            Dispatch(initializeNozzleEmissionKernel, particleCount);
            SeedLifecycleCountersAfterInitialize();
            emissionWriteIndex = 0;
        }
        else
        {
            simulationDomain = GpuSphSimulationDomain.OpenWorldWithBounds;
            Initialize();
        }
    }

    public void EmitFromNozzle(
        Vector3 localPosition,
        Vector3 localDirection,
        Vector3 localInheritedVelocity,
        int emitCount,
        float emitterRadius,
        float emitterSpeed,
        float emitterSpread,
        float emitterLifetime,
        bool allowNozzleExit)
    {
        if (!initialized || !kernelsReady || !BuffersValid || emitCount <= 0)
        {
            return;
        }

        int clampedEmitCount = Mathf.Clamp(emitCount, 1, particleCount);
        Vector3 direction = localDirection.sqrMagnitude > 0.000001f ? localDirection.normalized : Vector3.down;

        SetCommonShaderParameters();
        sphComputeShader.SetVector("_EmitterLocalPosition", localPosition);
        sphComputeShader.SetVector("_EmitterDirection", direction);
        sphComputeShader.SetVector("_EmitterInheritedVelocity", localInheritedVelocity);
        sphComputeShader.SetFloat("_EmitterRadius", Mathf.Max(0.001f, emitterRadius));
        sphComputeShader.SetFloat("_EmitterSpeed", Mathf.Max(0f, emitterSpeed));
        sphComputeShader.SetFloat("_EmitterSpread", Mathf.Max(0f, emitterSpread));
        lastEmitterLifetime = Mathf.Max(0.1f, emitterLifetime);
        sphComputeShader.SetFloat("_EmitterLifetime", lastEmitterLifetime);
        sphComputeShader.SetInt("_EmitterStartIndex", emissionWriteIndex);
        sphComputeShader.SetInt("_EmitCount", clampedEmitCount);
        sphComputeShader.SetInt("_EmitterSeed", Mathf.Abs(Time.frameCount * 73856093 + emissionWriteIndex));
        sphComputeShader.SetInt("_AllowNozzleExit", allowNozzleExit ? 1 : 0);

        Dispatch(emitFromNozzleKernel, clampedEmitCount);
        emissionWriteIndex = (emissionWriteIndex + clampedEmitCount) % Mathf.Max(1, particleCount);
    }

    private bool CacheKernels()
    {
        kernelsReady =
            TryFindKernel(SphKernelNames.InitializeParticles, ref initializeKernel) &&
            TryFindKernel(SphKernelNames.InitializeBucketVolume, ref initializeBucketVolumeKernel) &&
            TryFindKernel(SphKernelNames.InitializeNozzleEmission, ref initializeNozzleEmissionKernel) &&
            TryFindKernel(SphKernelNames.ClearGrid, ref clearGridKernel) &&
            TryFindKernel(SphKernelNames.BuildGrid, ref buildGridKernel) &&
            TryFindKernel(SphKernelNames.ComputeDensityPressure, ref densityPressureKernel) &&
            TryFindKernel(SphKernelNames.ComputeForces, ref forcesKernel) &&
            TryFindKernel(SphKernelNames.Integrate, ref integrateKernel) &&
            TryFindKernel(SphKernelNames.HandleBoxCollisions, ref collisionKernel) &&
            TryFindKernel(SphKernelNames.EmitFromNozzle, ref emitFromNozzleKernel) &&
            TryFindKernel(SphKernelNames.HandleBucketCollisions, ref bucketCollisionKernel);

        return kernelsReady;
    }

    private void ConfigureGrid()
    {
        GpuSphSettings settings = runtimeSettings;
        Vector3 boundsSize = ResolveBoundsSize(settings);
        cellSize = Mathf.Max(settings.smoothingLength * settings.cellSizeMultiplier, 0.01f);
        gridDimensions = new Vector3Int(
            Mathf.Max(1, Mathf.CeilToInt(boundsSize.x / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(boundsSize.y / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(boundsSize.z / cellSize))
        );
        gridCellCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;

        float fillVolume = boundsSize.x * 0.72f * boundsSize.y * 0.62f * boundsSize.z * 0.72f;
        float fitSpacing = Mathf.Pow(Mathf.Max(fillVolume / Mathf.Max(1, particleCount), 0.000001f), 1f / 3f);
        initialSpacing = Mathf.Clamp(fitSpacing * 0.96f, settings.particleRadius * 0.25f, settings.particleRadius * 2.05f);
        float spacing = Mathf.Max(initialSpacing, 0.001f);
        int nx = Mathf.Max(1, Mathf.FloorToInt(boundsSize.x * 0.72f / spacing));
        int nz = Mathf.Max(1, Mathf.FloorToInt(boundsSize.z * 0.72f / spacing));
        int ny = Mathf.Max(1, Mathf.CeilToInt(particleCount / (float)(nx * nz)));
        initGridDimensions = new Vector3(nx, ny, nz);
        particleDispatchGroupCount = GpuSphBufferUtility.DispatchGroups(particleCount, 256);
        gridDispatchGroupCount = GpuSphBufferUtility.DispatchGroups(gridCellCount, 256);
    }

    private void AllocateBuffers()
    {
        particleBuffer = new ComputeBuffer(particleCount, GpuSphBufferUtility.ParticleStrideBytes, ComputeBufferType.Structured);
        forceBuffer = new ComputeBuffer(particleCount, GpuSphBufferUtility.ForceStrideBytes, ComputeBufferType.Structured);
        cellCountsBuffer = new ComputeBuffer(gridCellCount, GpuSphBufferUtility.IntStrideBytes, ComputeBufferType.Structured);
        cellParticlesBuffer = new ComputeBuffer(gridCellCount * maxParticlesPerCell, GpuSphBufferUtility.IntStrideBytes, ComputeBufferType.Structured);
        overflowCountersBuffer = new ComputeBuffer(1, GpuSphBufferUtility.IntStrideBytes, ComputeBufferType.Structured);
        lifecycleCountersBuffer = new ComputeBuffer(3, GpuSphBufferUtility.IntStrideBytes, ComputeBufferType.Structured);
        overflowReadbackScratch[0] = 0;
        overflowCountersBuffer.SetData(overflowReadbackScratch);
        lifecycleReadbackScratch[0] = 0;
        lifecycleReadbackScratch[1] = particleCount;
        lifecycleReadbackScratch[2] = 0;
        lifecycleCountersBuffer.SetData(lifecycleReadbackScratch);

        SetBuffersForKernel(initializeKernel);
        SetBuffersForKernel(initializeBucketVolumeKernel);
        SetBuffersForKernel(initializeNozzleEmissionKernel);
        SetBuffersForKernel(clearGridKernel);
        SetBuffersForKernel(buildGridKernel);
        SetBuffersForKernel(densityPressureKernel);
        SetBuffersForKernel(forcesKernel);
        SetBuffersForKernel(integrateKernel);
        SetBuffersForKernel(collisionKernel);
        SetBuffersForKernel(emitFromNozzleKernel);
        SetBuffersForKernel(bucketCollisionKernel);

        long particleBytes =
            (long)particleCount * GpuSphBufferUtility.ParticleStrideBytes +
            (long)particleCount * GpuSphBufferUtility.ForceStrideBytes;
        long gridBytes =
            (long)gridCellCount * GpuSphBufferUtility.IntStrideBytes +
            (long)gridCellCount * maxParticlesPerCell * GpuSphBufferUtility.IntStrideBytes +
            GpuSphBufferUtility.IntStrideBytes +
            3L * GpuSphBufferUtility.IntStrideBytes;

        estimatedParticleBufferMemoryMb = GpuSphBufferUtility.BytesToMegabytes(particleBytes);
        estimatedGridMemoryMb = GpuSphBufferUtility.BytesToMegabytes(gridBytes);
        estimatedTotalGpuMemoryMb = estimatedParticleBufferMemoryMb + estimatedGridMemoryMb;
        gridOverflowCount = 0;
        activeParticleCount = 0;
        inactiveParticleCount = particleCount;
        totalEmittedParticleCount = 0;
        overflowReadbackTimer = overflowReadbackInterval;
    }

    private void SetBuffersForKernel(int kernel)
    {
        sphComputeShader.SetBuffer(kernel, "_Particles", particleBuffer);
        sphComputeShader.SetBuffer(kernel, "_Forces", forceBuffer);
        sphComputeShader.SetBuffer(kernel, "_CellCounts", cellCountsBuffer);
        sphComputeShader.SetBuffer(kernel, "_CellParticles", cellParticlesBuffer);
        sphComputeShader.SetBuffer(kernel, "_OverflowCounters", overflowCountersBuffer);
        sphComputeShader.SetBuffer(kernel, "_LifecycleCounters", lifecycleCountersBuffer);
    }

    private void SetCommonShaderParameters()
    {
        GpuSphSettings settings = runtimeSettings;
        Vector3 gravity = ResolveLocalGravity(settings);
        Vector3 boundsSize = ResolveBoundsSize(settings);

        sphComputeShader.SetInt("_ParticleCount", particleCount);
        sphComputeShader.SetInt("_GridCellCount", gridCellCount);
        sphComputeShader.SetInts("_GridDims", gridDimensions.x, gridDimensions.y, gridDimensions.z);
        sphComputeShader.SetInt("_MaxParticlesPerCell", maxParticlesPerCell);
        sphComputeShader.SetFloat("_CellSize", cellSize);
        sphComputeShader.SetFloat("_InitialSpacing", initialSpacing);
        sphComputeShader.SetVector("_BoundsSize", boundsSize);
        sphComputeShader.SetFloat("_ParticleRadius", settings.particleRadius);
        sphComputeShader.SetFloat("_SmoothingLength", settings.smoothingLength);
        sphComputeShader.SetFloat("_ParticleMass", settings.particleMass);
        sphComputeShader.SetFloat("_RestDensity", settings.restDensity);
        sphComputeShader.SetFloat("_Stiffness", settings.stiffness);
        sphComputeShader.SetFloat("_Viscosity", settings.viscosity);
        sphComputeShader.SetVector("_Gravity", gravity);
        sphComputeShader.SetFloat("_Damping", settings.damping);
        sphComputeShader.SetFloat("_MaxVelocity", settings.maxVelocity);
        sphComputeShader.SetFloat("_CollisionDamping", settings.collisionDamping);
        sphComputeShader.SetFloat("_TangentDamping", settings.tangentDamping);
        sphComputeShader.SetFloat("_WallOffset", settings.wallOffset);
        sphComputeShader.SetInt("_DeactivateOutOfBounds", simulationDomain == GpuSphSimulationDomain.OpenWorldWithBounds ? 1 : 0);

        SetBucketShaderParameters(boundsSize);
    }

    private void StepSimulation()
    {
        if (!kernelsReady || !BuffersValid)
        {
            return;
        }

        SetCommonShaderParameters();
        GpuSphSettings settings = runtimeSettings;
        float dt = settings.timestep / Mathf.Max(1, settings.substeps);
        sphComputeShader.SetFloat("_DeltaTime", dt);

        for (int i = 0; i < settings.substeps; i++)
        {
            Dispatch(clearGridKernel, gridCellCount);
            Dispatch(buildGridKernel, particleCount);
            Dispatch(densityPressureKernel, particleCount);
            Dispatch(forcesKernel, particleCount);
            Dispatch(integrateKernel, particleCount);
            DispatchCollisionKernel();
        }

        ReadbackOverflowCounterIfDue();
        UpdateDebugStats();
    }

    private void DispatchInitialState()
    {
        if (simulationDomain == GpuSphSimulationDomain.BucketCylinder)
        {
            Dispatch(initializeBucketVolumeKernel, particleCount);
            return;
        }

        if (simulationDomain == GpuSphSimulationDomain.OpenWorldWithBounds)
        {
            Dispatch(initializeNozzleEmissionKernel, particleCount);
            return;
        }

        Dispatch(initializeKernel, particleCount);
    }

    private void SeedLifecycleCountersAfterInitialize()
    {
        if (lifecycleCountersBuffer == null)
        {
            return;
        }

        activeParticleCount = simulationDomain == GpuSphSimulationDomain.OpenWorldWithBounds ? 0 : particleCount;
        inactiveParticleCount = particleCount - activeParticleCount;
        totalEmittedParticleCount = 0;
        lifecycleReadbackScratch[0] = activeParticleCount;
        lifecycleReadbackScratch[1] = inactiveParticleCount;
        lifecycleReadbackScratch[2] = totalEmittedParticleCount;
        lifecycleCountersBuffer.SetData(lifecycleReadbackScratch);
        emissionWriteIndex = 0;
    }

    private void DispatchCollisionKernel()
    {
        if (simulationDomain == GpuSphSimulationDomain.BucketCylinder)
        {
            Dispatch(bucketCollisionKernel, particleCount);
            return;
        }

        Dispatch(collisionKernel, particleCount);
    }

    private Vector3 ResolveLocalGravity(GpuSphSettings settings)
    {
        if (simulationDomain == GpuSphSimulationDomain.FluidBox && fluidBox != null)
        {
            return fluidBox.LocalGravity;
        }

        if (simulationDomain == GpuSphSimulationDomain.BucketCylinder && bucketCollisionProvider != null)
        {
            return bucketCollisionProvider.LocalGravity;
        }

        return WorldToSimulationDirection(settings.gravity);
    }

    private Vector3 ResolveBoundsSize(GpuSphSettings settings)
    {
        if (simulationDomain == GpuSphSimulationDomain.FluidBox && fluidBox != null)
        {
            return fluidBox.BoundsSize;
        }

        if ((simulationDomain == GpuSphSimulationDomain.BucketCylinder ||
                simulationDomain == GpuSphSimulationDomain.OpenWorldWithBounds) &&
            bucketCollisionProvider != null)
        {
            return bucketCollisionProvider.ExternalBoundsSize;
        }

        return settings.boundsSize;
    }

    private void SetBucketShaderParameters(Vector3 boundsSize)
    {
        BucketSphCollisionProvider provider = bucketCollisionProvider;
        Vector3 nozzleSimulationPosition = new Vector3(-0.14f, -1f, 0f);
        Vector3 emitterDirection = Vector3.down;
        Vector3 inheritedVelocity = Vector3.zero;

        if (provider != null)
        {
            if (simulationDomain == GpuSphSimulationDomain.BucketCylinder)
            {
                nozzleSimulationPosition = provider.NozzleLocalPosition;
                emitterDirection = provider.nozzlePoint != null
                    ? provider.BucketRoot.InverseTransformDirection(-provider.nozzlePoint.up)
                    : Vector3.down;
                inheritedVelocity = provider.LocalBucketVelocity;
            }
            else
            {
                Vector3 worldNozzlePosition = provider.nozzlePoint != null
                    ? provider.nozzlePoint.position
                    : provider.BucketRoot.TransformPoint(provider.NozzleLocalPosition);
                Vector3 worldDirection = provider.nozzlePoint != null ? -provider.nozzlePoint.up : Vector3.down;
                nozzleSimulationPosition = WorldToSimulationPosition(worldNozzlePosition);
                emitterDirection = WorldToSimulationDirection(worldDirection);
                inheritedVelocity = provider.motionDataProvider != null
                    ? WorldToSimulationVelocity(provider.motionDataProvider.WorldVelocity)
                    : Vector3.zero;
            }
        }

        sphComputeShader.SetVector("_ExternalBoundsSize", boundsSize);
        sphComputeShader.SetVector("_BucketLocalCenter", provider != null ? provider.LocalCenter : Vector3.zero);
        sphComputeShader.SetFloat("_BucketRadius", provider != null ? provider.Radius : 0.45f);
        sphComputeShader.SetFloat("_BucketHeight", provider != null ? provider.Height : 0.9f);
        sphComputeShader.SetFloat("_BucketBottomOffset", provider != null ? provider.BottomOffset : -0.45f);
        sphComputeShader.SetFloat("_BucketWallThickness", provider != null ? provider.WallThickness : 0.035f);
        sphComputeShader.SetVector("_BucketNozzleLocalPosition", nozzleSimulationPosition);
        sphComputeShader.SetFloat("_BucketNozzleRadius", provider != null ? provider.NozzleRadius : 0.04f);
        sphComputeShader.SetInt("_AllowNozzleExit", provider != null && provider.allowNozzleExit ? 1 : 0);
        sphComputeShader.SetVector("_EmitterLocalPosition", nozzleSimulationPosition);
        sphComputeShader.SetVector("_EmitterDirection", emitterDirection.sqrMagnitude > 0.000001f ? emitterDirection.normalized : Vector3.down);
        sphComputeShader.SetVector("_EmitterInheritedVelocity", inheritedVelocity);
        sphComputeShader.SetFloat("_EmitterRadius", provider != null ? provider.NozzleRadius : 0.04f);
        sphComputeShader.SetFloat("_EmitterSpeed", 2.2f);
        sphComputeShader.SetFloat("_EmitterSpread", 0.35f);
        sphComputeShader.SetFloat("_EmitterLifetime", Mathf.Max(0.1f, lastEmitterLifetime));
    }

    private void Dispatch(int kernel, int count)
    {
        sphComputeShader.Dispatch(kernel, GpuSphBufferUtility.DispatchGroups(count, 256), 1, 1);
    }

    private void UpdateDebugStats()
    {
        if (debugStats != null)
        {
            debugStats.RecordFrom(this);
        }
    }

    private void EnsureRuntimeSettings(bool forceClone)
    {
        if (settingsTemplate == null)
        {
            return;
        }

        if (runtimeSettings != null && !forceClone)
        {
            return;
        }

        runtimeSettings = Instantiate(settingsTemplate);
        runtimeSettings.name = settingsTemplate.name + "_Runtime";
        runtimeSettings.hideFlags = HideFlags.DontSave;
    }

    private bool TryFindKernel(string kernelName, ref int kernelIndex)
    {
        try
        {
            kernelIndex = sphComputeShader.FindKernel(kernelName);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[GpuSphSolver] Missing compute kernel '" + kernelName + "': " + exception.Message, this);
            return false;
        }
    }

    private void ReadbackOverflowCounterIfDue()
    {
        if (overflowCountersBuffer == null)
        {
            return;
        }

        overflowReadbackTimer += Time.unscaledDeltaTime;
        if (overflowReadbackTimer < overflowReadbackInterval)
        {
            return;
        }

        overflowReadbackTimer = 0f;
        overflowCountersBuffer.GetData(overflowReadbackScratch);
        gridOverflowCount = overflowReadbackScratch[0];

        if (lifecycleCountersBuffer != null)
        {
            lifecycleCountersBuffer.GetData(lifecycleReadbackScratch);
            activeParticleCount = lifecycleReadbackScratch[0];
            inactiveParticleCount = lifecycleReadbackScratch[1];
            totalEmittedParticleCount = lifecycleReadbackScratch[2];
        }
    }

    private void WarnOnce(string message)
    {
        if (warnedMissingConfiguration)
        {
            return;
        }

        warnedMissingConfiguration = true;
        Debug.LogWarning(message, this);
    }

    private void ReleaseBuffers()
    {
        GpuSphBufferUtility.ReleaseBuffer(ref particleBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref forceBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref cellCountsBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref cellParticlesBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref overflowCountersBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref lifecycleCountersBuffer);
        initialized = false;
    }
}
