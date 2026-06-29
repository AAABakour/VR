using UnityEngine;

public class GpuSphDebugStats : MonoBehaviour
{
    public int simulatedParticleCount;
    public int renderedParticleCount;
    public int renderStride;
    public float estimatedGpuMemoryMb;
    public string activeMode;
    public Vector3Int gridDimensions;
    public Vector3 boundsSize;
    public int substeps;
    public float smoothingLength;
    public float restDensity;
    public float viscosity;
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
        renderedParticleCount = solver.RenderedParticleCount;
        renderStride = solver.RenderStride;
        estimatedGpuMemoryMb = solver.EstimatedGpuMemoryMb;
        activeMode = solver.ActiveModeLabel;
        gridDimensions = solver.GridDimensions;
        boundsSize = solver.BoundsSize;
        substeps = solver.Substeps;
        smoothingLength = solver.SmoothingLength;
        restDensity = solver.RestDensity;
        viscosity = solver.Viscosity;
    }
}
