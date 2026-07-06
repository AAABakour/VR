using UnityEngine;

[CreateAssetMenu(menuName = "VR Paint/SPH Simulation Settings", fileName = "SphSimulationSettings")]
public class SphSimulationSettings : ScriptableObject
{
    [Header("Target Scale")]
    [Min(1024)] public int targetParticleCount = 1000000;
    [Min(1)] public int renderStride = 12;
    [Min(128)] public int collisionBatchSize = 8192;

    [Header("GPU Solver Budget")]
    [Range(0.004f, 0.033f)] public float simulationTimeStep = 0.0083333f;
    [Range(1, 8)] public int solverSubsteps = 2;
    [Range(16, 512)] public int computeThreadGroupSize = 256;
    [Range(32, 512)] public int spatialGridResolution = 192;

    [Header("Physical Paint Model")]
    [Range(0.001f, 0.08f)] public float particleRadius = 0.012f;
    [Range(100f, 2200f)] public float restDensity = 1050f;
    [Range(0.01f, 16f)] public float viscosity = 4.8f;
    [Range(0f, 1f)] public float surfaceTension = 0.28f;
    [Range(0f, 1f)] public float adhesion = 0.42f;

    [Header("Rendering")]
    [Range(0.01f, 0.35f)] public float renderParticleScale = 0.055f;
    [Range(0.0f, 1.0f)] public float fluidSmoothness = 0.92f;
    [Range(0f, 2f)] public float thicknessToAlpha = 0.85f;

    [Header("CPU Fallback Guardrails")]
    [Range(64, 3000)] public int legacyCpuFallbackParticles = 384;
    [Range(0, 256)] public int legacyVisualDroplets = 24;
    public bool disableLegacyParticleInteractions = true;

    public int VisibleParticleBudget
    {
        get
        {
            return Mathf.Max(1, Mathf.CeilToInt(targetParticleCount / Mathf.Max(1f, renderStride)));
        }
    }

    public float EstimatedParticleBufferMegabytes
    {
        get
        {
            // Position/velocity/density/pressure/color/lifetime/alignment padding, intentionally conservative.
            const float bytesPerParticle = 96f;
            return targetParticleCount * bytesPerParticle / (1024f * 1024f);
        }
    }

    public float EstimatedGridBufferMegabytes
    {
        get
        {
            float cells = spatialGridResolution * spatialGridResolution * spatialGridResolution;
            const float bytesPerCell = 8f;
            return cells * bytesPerCell / (1024f * 1024f);
        }
    }

    public string BuildSummary()
    {
        return "SPH target " + targetParticleCount.ToString("N0") +
               " particles | visible ~" + VisibleParticleBudget.ToString("N0") +
               " | stride " + renderStride +
               " | collision batch " + collisionBatchSize.ToString("N0") +
               " | estimated GPU buffers " + (EstimatedParticleBufferMegabytes + EstimatedGridBufferMegabytes).ToString("0.0") + " MB";
    }

    public void Sanitize()
    {
        targetParticleCount = Mathf.Clamp(targetParticleCount, 1024, 4000000);
        renderStride = Mathf.Clamp(renderStride, 1, 256);
        collisionBatchSize = Mathf.Clamp(collisionBatchSize, 128, 131072);
        solverSubsteps = Mathf.Clamp(solverSubsteps, 1, 8);
        computeThreadGroupSize = Mathf.Clamp(computeThreadGroupSize, 16, 512);
        spatialGridResolution = Mathf.Clamp(spatialGridResolution, 32, 512);
        legacyCpuFallbackParticles = Mathf.Clamp(legacyCpuFallbackParticles, 64, 3000);
        legacyVisualDroplets = Mathf.Clamp(legacyVisualDroplets, 0, 256);
    }
}
