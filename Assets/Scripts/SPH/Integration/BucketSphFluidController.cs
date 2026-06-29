using UnityEngine;

[DefaultExecutionOrder(90)]
public class BucketSphFluidController : MonoBehaviour
{
    public GpuSphSolver solver;
    public GpuSphParticleRenderer particleRenderer;
    public BucketSphCollisionProvider collisionProvider;
    public BucketSphNozzleEmitter nozzleEmitter;
    public BucketRigController bucketRigController;
    public BucketMotionDataProvider bucketMotionDataProvider;

    [Header("Mode")]
    public BucketSphMode startupMode = BucketSphMode.InternalBucketFluid;
    public bool initializeOnStart = true;
    public bool disableLegacyPaintOnStart = true;
    public PaintEmitter legacyPaintEmitter;
    public PaintParticleSimulator legacyPaintParticleSimulator;

    [Header("Profiles")]
    public SimulationProfile legacyPrototypeProfile;
    public SimulationProfile debugProfile;
    public SimulationProfile presentationProfile;
    public SimulationProfile professorBenchmarkProfile;
    public SimulationProfile vrSafeProfile;
    public SimulationProfile startupProfile;

    private BucketSphMode currentMode;
    private SimulationProfile activeProfile;

    public BucketSphMode CurrentMode
    {
        get { return currentMode; }
    }

    public SimulationProfile ActiveProfile
    {
        get { return activeProfile; }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (disableLegacyPaintOnStart)
        {
            SetLegacyPaintEnabled(false);
        }

        if (initializeOnStart)
        {
            ApplyProfile(startupProfile != null ? startupProfile : debugProfile);
            SetMode(startupMode, true);
        }
    }

    [ContextMenu("Resolve Bucket SPH Controller References")]
    public void ResolveReferences()
    {
        if (solver == null)
        {
            solver = GetComponent<GpuSphSolver>();
        }

        if (particleRenderer == null)
        {
            particleRenderer = GetComponent<GpuSphParticleRenderer>();
        }

        if (collisionProvider == null)
        {
            collisionProvider = GetComponent<BucketSphCollisionProvider>();
        }

        if (nozzleEmitter == null)
        {
            nozzleEmitter = Object.FindFirstObjectByType<BucketSphNozzleEmitter>();
        }

        if (bucketRigController == null)
        {
            bucketRigController = Object.FindFirstObjectByType<BucketRigController>();
        }

        if (bucketMotionDataProvider == null)
        {
            bucketMotionDataProvider = Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }

        if (legacyPaintEmitter == null)
        {
            legacyPaintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (legacyPaintParticleSimulator == null)
        {
            legacyPaintParticleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        }

        if (collisionProvider != null)
        {
            collisionProvider.bucketRigController = bucketRigController;
            collisionProvider.motionDataProvider = bucketMotionDataProvider;
            collisionProvider.ResolveReferences();
        }

        if (nozzleEmitter != null)
        {
            nozzleEmitter.solver = solver;
            nozzleEmitter.collisionProvider = collisionProvider;
            nozzleEmitter.bucketMotionDataProvider = bucketMotionDataProvider;
            if (collisionProvider != null)
            {
                nozzleEmitter.nozzlePoint = collisionProvider.nozzlePoint;
            }
        }

        if (particleRenderer != null)
        {
            particleRenderer.solver = solver;
            particleRenderer.fluidBox = null;
        }
    }

    public void SetMode(BucketSphMode mode)
    {
        SetMode(mode, true);
    }

    public void SetMode(BucketSphMode mode, bool resetFluid)
    {
        currentMode = mode;
        ConfigureSolverDomain();
        ConfigureEmitter();

        if (resetFluid && solver != null)
        {
            solver.Initialize();
        }
    }

    public void ApplyProfile(SimulationProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        activeProfile = profile;
        if (solver != null)
        {
            solver.ApplySimulationProfile(profile);
        }
    }

    public void ResetBucketSph()
    {
        if (nozzleEmitter != null)
        {
            nozzleEmitter.ResetEmitter();
        }

        if (solver != null)
        {
            solver.Initialize();
        }
    }

    public void SetPaused(bool paused)
    {
        if (solver != null)
        {
            solver.simulate = !paused;
        }

        if (nozzleEmitter != null)
        {
            nozzleEmitter.enabled = !paused;
        }
    }

    public void SetLegacyPaintEnabled(bool enabled)
    {
        if (legacyPaintEmitter != null)
        {
            legacyPaintEmitter.enabled = enabled;
        }

        if (legacyPaintParticleSimulator != null)
        {
            legacyPaintParticleSimulator.enabled = enabled;
        }
    }

    public void CycleMode()
    {
        int next = ((int)currentMode + 1) % 4;
        SetMode((BucketSphMode)next, true);
    }

    public void UseDebugProfile()
    {
        ApplyProfile(debugProfile);
    }

    public void UsePresentationProfile()
    {
        ApplyProfile(presentationProfile);
    }

    public void UseProfessorBenchmarkProfile()
    {
        ApplyProfile(professorBenchmarkProfile);
    }

    public void UseVrSafeProfile()
    {
        ApplyProfile(vrSafeProfile);
    }

    private void ConfigureSolverDomain()
    {
        if (solver == null)
        {
            return;
        }

        solver.bucketCollisionProvider = collisionProvider;
        solver.fluidBox = null;

        switch (currentMode)
        {
            case BucketSphMode.NozzleEmission:
            case BucketSphMode.DebugStaticEmission:
                solver.simulationDomain = GpuSphSimulationDomain.OpenWorldWithBounds;
                break;
            default:
                solver.simulationDomain = GpuSphSimulationDomain.BucketCylinder;
                break;
        }
    }

    private void ConfigureEmitter()
    {
        if (nozzleEmitter == null)
        {
            return;
        }

        bool emitting =
            currentMode == BucketSphMode.NozzleEmission ||
            currentMode == BucketSphMode.InternalAndEmission ||
            currentMode == BucketSphMode.DebugStaticEmission;

        nozzleEmitter.emitOnUpdate = emitting;
        nozzleEmitter.allowNozzleExit = currentMode != BucketSphMode.InternalBucketFluid;

        if (collisionProvider != null)
        {
            collisionProvider.allowNozzleExit = nozzleEmitter.allowNozzleExit;
        }

        if (currentMode == BucketSphMode.DebugStaticEmission)
        {
            nozzleEmitter.inheritedBucketVelocity = 0f;
        }
    }
}
