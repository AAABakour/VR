using UnityEngine;

public class FluidBoxBenchmarkController : MonoBehaviour
{
    public GpuSphSolver solver;
    public FluidBoxController fluidBox;
    public FluidBoxMotionController motionController;
    public GpuSphDebugStats debugStats;
    public SimulationProfile debugProfile;
    public SimulationProfile presentationProfile;
    public SimulationProfile professorBenchmarkProfile;
    public SimulationProfile vrSafeProfile;

    private void Start()
    {
        ResolveReferences();
    }

    public void ResetFluid()
    {
        ResolveReferences();
        if (fluidBox != null)
        {
            fluidBox.ResetMotionSample();
        }

        if (motionController != null)
        {
            motionController.ResetMotion();
        }

        if (solver != null)
        {
            solver.ResetSimulation();
        }
    }

    public void ApplyDebugMode()
    {
        ApplyProfile(debugProfile);
    }

    public void ApplyPresentationMode()
    {
        ApplyProfile(presentationProfile);
    }

    public void ApplyProfessorBenchmarkMode()
    {
        ApplyProfile(professorBenchmarkProfile);
    }

    public void ApplyVRSafeMode()
    {
        ApplyProfile(vrSafeProfile);
    }

    public void ToggleBoxMotion()
    {
        ResolveReferences();
        if (motionController != null)
        {
            motionController.ToggleMotion();
        }
    }

    public void CycleBoxMotionMode()
    {
        ResolveReferences();
        if (motionController != null)
        {
            motionController.CycleMotionMode();
        }
    }

    public void ApplyProfile(SimulationProfile profile)
    {
        ResolveReferences();
        if (solver != null && profile != null)
        {
            solver.ApplySimulationProfile(profile);
        }
    }

    private void ResolveReferences()
    {
        if (solver == null)
        {
            solver = Object.FindFirstObjectByType<GpuSphSolver>();
        }

        if (fluidBox == null)
        {
            fluidBox = Object.FindFirstObjectByType<FluidBoxController>();
        }

        if (motionController == null)
        {
            motionController = Object.FindFirstObjectByType<FluidBoxMotionController>();
        }

        if (debugStats == null)
        {
            debugStats = Object.FindFirstObjectByType<GpuSphDebugStats>();
        }
    }
}
