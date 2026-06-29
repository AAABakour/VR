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

    [Header("Emission Defaults")]
    [Range(0f, 1f)]
    public float defaultInheritedBucketVelocity = 0.55f;

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
            SetMode(startupMode, false);
            ApplyProfile(startupProfile != null ? startupProfile : debugProfile);
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
        ResolveReferences();
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

        ResolveReferences();
        activeProfile = profile;
        ConfigureSolverDomain();
        ConfigureEmitter();

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

    public void SetInternalFluidMode()
    {
        SetMode(BucketSphMode.InternalBucketFluid, true);
    }

    public void SetNozzleEmissionMode()
    {
        SetMode(BucketSphMode.NozzleEmission, true);
    }

    public void SetInternalAndEmissionMode()
    {
        SetMode(BucketSphMode.InternalAndEmission, true);
    }

    public void SetDebugStaticEmissionMode()
    {
        SetMode(BucketSphMode.DebugStaticEmission, true);
    }

    public void ToggleEmission()
    {
        ResolveReferences();

        bool enableEmission = nozzleEmitter == null || !nozzleEmitter.emitOnUpdate;
        if (enableEmission)
        {
            BucketSphMode emissionMode = currentMode == BucketSphMode.InternalBucketFluid
                ? BucketSphMode.InternalAndEmission
                : currentMode;
            SetMode(emissionMode, true);

            if (nozzleEmitter != null)
            {
                nozzleEmitter.emitOnUpdate = true;
            }

            return;
        }

        if (nozzleEmitter != null)
        {
            nozzleEmitter.emitOnUpdate = false;
            nozzleEmitter.ResetEmitter();
        }

        if (currentMode == BucketSphMode.NozzleEmission ||
            currentMode == BucketSphMode.InternalAndEmission ||
            currentMode == BucketSphMode.DebugStaticEmission)
        {
            SetMode(BucketSphMode.InternalBucketFluid, true);
        }
    }

    public void UseDebugProfile()
    {
        ApplyProfile(debugProfile);
    }

    public void ApplyDebugProfile()
    {
        UseDebugProfile();
    }

    public void UsePresentationProfile()
    {
        ApplyProfile(presentationProfile);
    }

    public void ApplyPresentationProfile()
    {
        UsePresentationProfile();
    }

    public void UseProfessorBenchmarkProfile()
    {
        ApplyProfile(professorBenchmarkProfile);
    }

    public void ApplyProfessorBenchmarkProfile()
    {
        UseProfessorBenchmarkProfile();
    }

    public void UseVrSafeProfile()
    {
        ApplyProfile(vrSafeProfile);
    }

    public void ApplyVrSafeProfile()
    {
        UseVrSafeProfile();
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
        nozzleEmitter.inheritedBucketVelocity = currentMode == BucketSphMode.DebugStaticEmission
            ? 0f
            : defaultInheritedBucketVelocity;

        if (collisionProvider != null)
        {
            collisionProvider.allowNozzleExit = nozzleEmitter.allowNozzleExit;
        }
    }
}
