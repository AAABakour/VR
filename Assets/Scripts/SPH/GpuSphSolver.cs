using UnityEngine;

[DefaultExecutionOrder(80)]
public class GpuSphSolver : MonoBehaviour
{
    public ComputeShader sphComputeShader;
    public GpuSphSettings settings;
    public FluidBoxController fluidBox;
    public GpuSphDebugStats debugStats;
    public bool initializeOnStart = true;
    public bool simulate = true;
    public bool applySettingsBoundsToBox = true;

    private ComputeBuffer particleBuffer;
    private ComputeBuffer forceBuffer;
    private ComputeBuffer cellCountsBuffer;
    private ComputeBuffer cellParticlesBuffer;

    private int initializeKernel;
    private int clearGridKernel;
    private int buildGridKernel;
    private int densityPressureKernel;
    private int forcesKernel;
    private int integrateKernel;
    private int collisionKernel;

    private int particleCount;
    private int renderStride;
    private int gridCellCount;
    private int maxParticlesPerCell;
    private Vector3Int gridDimensions;
    private Vector3 initGridDimensions;
    private float cellSize;
    private float initialSpacing;
    private float estimatedGpuMemoryMb;
    private bool initialized;

    public ComputeBuffer ParticleBuffer
    {
        get { return particleBuffer; }
    }

    public int ParticleCount
    {
        get { return particleCount; }
    }

    public int RenderStride
    {
        get { return renderStride; }
    }

    public int RenderedParticleCount
    {
        get { return Mathf.CeilToInt(particleCount / (float)Mathf.Max(1, renderStride)); }
    }

    public float EstimatedGpuMemoryMb
    {
        get { return estimatedGpuMemoryMb; }
    }

    public string ActiveModeLabel
    {
        get { return settings != null ? settings.modeLabel : "Unconfigured"; }
    }

    public Vector3Int GridDimensions
    {
        get { return gridDimensions; }
    }

    public Vector3 BoundsSize
    {
        get { return settings != null ? settings.boundsSize : Vector3.zero; }
    }

    public int Substeps
    {
        get { return settings != null ? settings.substeps : 0; }
    }

    public float SmoothingLength
    {
        get { return settings != null ? settings.smoothingLength : 0f; }
    }

    public float RestDensity
    {
        get { return settings != null ? settings.restDensity : 0f; }
    }

    public float Viscosity
    {
        get { return settings != null ? settings.viscosity : 0f; }
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
        if (settings == null || sphComputeShader == null)
        {
            Debug.LogWarning("[GpuSphSolver] Missing settings or compute shader.", this);
            return;
        }

        settings.ClampValues();
        particleCount = settings.ClampedParticleCount;
        renderStride = settings.ClampedRenderStride;
        maxParticlesPerCell = Mathf.Max(1, settings.maxParticlesPerCell);

        if (fluidBox != null && applySettingsBoundsToBox)
        {
            fluidBox.boundsSize = settings.boundsSize;
        }

        CacheKernels();
        ConfigureGrid();
        ReleaseBuffers();
        AllocateBuffers();
        SetCommonShaderParameters();

        sphComputeShader.SetVector("_InitGridDims", new Vector4(initGridDimensions.x, initGridDimensions.y, initGridDimensions.z, 0f));
        Dispatch(initializeKernel, particleCount);

        initialized = true;
        UpdateDebugStats();
    }

    public void ResetSimulation()
    {
        Initialize();
    }

    public void ApplySettings(GpuSphSettings newSettings)
    {
        settings = newSettings;
        Initialize();
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        if (settings == null || profile == null)
        {
            return;
        }

        settings.ApplySimulationProfile(profile);
        Initialize();
    }

    private void CacheKernels()
    {
        initializeKernel = sphComputeShader.FindKernel(SphKernelNames.InitializeParticles);
        clearGridKernel = sphComputeShader.FindKernel(SphKernelNames.ClearGrid);
        buildGridKernel = sphComputeShader.FindKernel(SphKernelNames.BuildGrid);
        densityPressureKernel = sphComputeShader.FindKernel(SphKernelNames.ComputeDensityPressure);
        forcesKernel = sphComputeShader.FindKernel(SphKernelNames.ComputeForces);
        integrateKernel = sphComputeShader.FindKernel(SphKernelNames.Integrate);
        collisionKernel = sphComputeShader.FindKernel(SphKernelNames.HandleBoxCollisions);
    }

    private void ConfigureGrid()
    {
        cellSize = Mathf.Max(settings.smoothingLength * settings.cellSizeMultiplier, 0.01f);
        gridDimensions = new Vector3Int(
            Mathf.Max(1, Mathf.CeilToInt(settings.boundsSize.x / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(settings.boundsSize.y / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(settings.boundsSize.z / cellSize))
        );
        gridCellCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;

        float fillVolume = settings.boundsSize.x * 0.72f * settings.boundsSize.y * 0.62f * settings.boundsSize.z * 0.72f;
        float fitSpacing = Mathf.Pow(Mathf.Max(fillVolume / Mathf.Max(1, particleCount), 0.000001f), 1f / 3f);
        initialSpacing = Mathf.Clamp(fitSpacing * 0.96f, settings.particleRadius * 0.25f, settings.particleRadius * 2.05f);
        float spacing = Mathf.Max(initialSpacing, 0.001f);
        int nx = Mathf.Max(1, Mathf.FloorToInt(settings.boundsSize.x * 0.72f / spacing));
        int nz = Mathf.Max(1, Mathf.FloorToInt(settings.boundsSize.z * 0.72f / spacing));
        int ny = Mathf.Max(1, Mathf.CeilToInt(particleCount / (float)(nx * nz)));
        initGridDimensions = new Vector3(nx, ny, nz);
    }

    private void AllocateBuffers()
    {
        particleBuffer = new ComputeBuffer(particleCount, GpuSphBufferUtility.ParticleStrideBytes, ComputeBufferType.Structured);
        forceBuffer = new ComputeBuffer(particleCount, GpuSphBufferUtility.ForceStrideBytes, ComputeBufferType.Structured);
        cellCountsBuffer = new ComputeBuffer(gridCellCount, GpuSphBufferUtility.IntStrideBytes, ComputeBufferType.Structured);
        cellParticlesBuffer = new ComputeBuffer(gridCellCount * maxParticlesPerCell, GpuSphBufferUtility.IntStrideBytes, ComputeBufferType.Structured);

        SetBuffersForKernel(initializeKernel);
        SetBuffersForKernel(buildGridKernel);
        SetBuffersForKernel(densityPressureKernel);
        SetBuffersForKernel(forcesKernel);
        SetBuffersForKernel(integrateKernel);
        SetBuffersForKernel(collisionKernel);
        sphComputeShader.SetBuffer(clearGridKernel, "_CellCounts", cellCountsBuffer);

        long bytes =
            (long)particleCount * GpuSphBufferUtility.ParticleStrideBytes +
            (long)particleCount * GpuSphBufferUtility.ForceStrideBytes +
            (long)gridCellCount * GpuSphBufferUtility.IntStrideBytes +
            (long)gridCellCount * maxParticlesPerCell * GpuSphBufferUtility.IntStrideBytes;

        estimatedGpuMemoryMb = GpuSphBufferUtility.BytesToMegabytes(bytes);
    }

    private void SetBuffersForKernel(int kernel)
    {
        sphComputeShader.SetBuffer(kernel, "_Particles", particleBuffer);
        sphComputeShader.SetBuffer(kernel, "_Forces", forceBuffer);
        sphComputeShader.SetBuffer(kernel, "_CellCounts", cellCountsBuffer);
        sphComputeShader.SetBuffer(kernel, "_CellParticles", cellParticlesBuffer);
    }

    private void SetCommonShaderParameters()
    {
        Vector3 gravity = fluidBox != null ? fluidBox.LocalGravity : settings.gravity;
        Vector3 boundsSize = fluidBox != null ? fluidBox.BoundsSize : settings.boundsSize;

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
    }

    private void StepSimulation()
    {
        SetCommonShaderParameters();
        float dt = settings.timestep / Mathf.Max(1, settings.substeps);
        sphComputeShader.SetFloat("_DeltaTime", dt);

        for (int i = 0; i < settings.substeps; i++)
        {
            Dispatch(clearGridKernel, gridCellCount);
            Dispatch(buildGridKernel, particleCount);
            Dispatch(densityPressureKernel, particleCount);
            Dispatch(forcesKernel, particleCount);
            Dispatch(integrateKernel, particleCount);
            Dispatch(collisionKernel, particleCount);
        }

        UpdateDebugStats();
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

    private void ReleaseBuffers()
    {
        GpuSphBufferUtility.ReleaseBuffer(ref particleBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref forceBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref cellCountsBuffer);
        GpuSphBufferUtility.ReleaseBuffer(ref cellParticlesBuffer);
        initialized = false;
    }
}
