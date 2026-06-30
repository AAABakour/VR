using UnityEngine;

public class GpuSphDebugStats : MonoBehaviour
{
    public int simulatedParticleCount;
    public int requestedParticleCount;
    public int allocatedParticleCount;
    public int gpuBufferParticleCapacity;
    public int activeParticleCount;
    public int inactiveParticleCount;
    public int totalEmittedParticleCount;
    public int renderedParticleCount;
    public int allocatedRenderedCapacity;
    public int expectedRenderedParticleCount;
    public int renderStride;
    public int particleStrideBytes;
    public float estimatedGpuMemoryMb;
    public float estimatedParticleBufferMemoryMb;
    public float estimatedGridMemoryMb;
    public float estimatedTotalGpuMemoryMb;
    public string activeMode;
    public string runtimeProfileLabel;
    public string simulationDomain;
    public string simulationFrame;
    public bool isProfessorBenchmarkActive;
    public Vector3Int gridDimensions;
    public Vector3 boundsSize;
    public int maxParticlesPerCell;
    public int gridOverflowCount;
    public int particleDispatchGroupCount;
    public int gridDispatchGroupCount;
    public int substeps;
    public float timestep;
    public float smoothingLength;
    public float restDensity;
    public float viscosity;
    public bool solverInitialized;
    public bool buffersValid;
    public float emitterLifetime;
    public float fps;

    private float fpsAccumulator;
    private int fpsSamples;
    private float fpsTimer;

    private void Update()
    {
        if (Time.unscaledDeltaTime <= 0f)
        {
            return;
        }

        fpsAccumulator += 1f / Time.unscaledDeltaTime;
        fpsSamples++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= 0.5f)
        {
            fps = fpsAccumulator / Mathf.Max(1, fpsSamples);
            fpsAccumulator = 0f;
            fpsSamples = 0;
            fpsTimer = 0f;
        }
    }

    public void RecordFrom(GpuSphSolver solver)
    {
        if (solver == null)
        {
            return;
        }

        simulatedParticleCount = solver.ParticleCount;
        requestedParticleCount = solver.RequestedParticleCount;
        allocatedParticleCount = solver.AllocatedParticleCount;
        gpuBufferParticleCapacity = solver.GpuBufferParticleCapacity;
        activeParticleCount = solver.ActiveParticleCount;
        inactiveParticleCount = solver.InactiveParticleCount;
        totalEmittedParticleCount = solver.TotalEmittedParticleCount;
        renderedParticleCount = solver.RenderedParticleCount;
        allocatedRenderedCapacity = solver.AllocatedRenderedCapacity;
        expectedRenderedParticleCount = solver.ExpectedRenderedParticleCount;
        renderStride = solver.RenderStride;
        particleStrideBytes = solver.ParticleStrideBytes;
        estimatedGpuMemoryMb = solver.EstimatedGpuMemoryMb;
        estimatedParticleBufferMemoryMb = solver.EstimatedParticleBufferMemoryMb;
        estimatedGridMemoryMb = solver.EstimatedGridMemoryMb;
        estimatedTotalGpuMemoryMb = solver.EstimatedTotalGpuMemoryMb;
        activeMode = solver.ActiveModeLabel;
        runtimeProfileLabel = solver.RuntimeProfileLabel;
        simulationDomain = solver.SimulationDomainLabel;
        simulationFrame = solver.SimulationFrameLabel;
        isProfessorBenchmarkActive = solver.IsProfessorBenchmarkActive;
        gridDimensions = solver.GridDimensions;
        boundsSize = solver.BoundsSize;
        maxParticlesPerCell = solver.MaxParticlesPerCell;
        gridOverflowCount = solver.GridOverflowCount;
        particleDispatchGroupCount = solver.ParticleDispatchGroupCount;
        gridDispatchGroupCount = solver.GridDispatchGroupCount;
        substeps = solver.Substeps;
        timestep = solver.Timestep;
        smoothingLength = solver.SmoothingLength;
        restDensity = solver.RestDensity;
        viscosity = solver.Viscosity;
        solverInitialized = solver.IsInitialized;
        buffersValid = solver.BuffersValid;
        emitterLifetime = solver.LastEmitterLifetime;
    }
}
