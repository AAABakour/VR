using UnityEngine;

[CreateAssetMenu(
    fileName = "GpuSphSettings",
    menuName = "Swinging Paint Bucket/GPU SPH Settings"
)]
public class GpuSphSettings : ScriptableObject
{
    [Header("Mode")]
    public string modeLabel = "Debug";

    [Header("Particles")]
    [Min(1)]
    public int particleCount = 50000;
    [Min(1)]
    public int maxParticleCount = 1000000;
    [Min(0.001f)]
    public float particleRadius = 0.025f;
    [Min(0.001f)]
    public float smoothingLength = 0.12f;
    [Min(0.000001f)]
    public float particleMass = 0.02f;

    [Header("SPH")]
    [Min(0.001f)]
    public float restDensity = 1000f;
    [Min(0f)]
    public float stiffness = 250f;
    [Min(0f)]
    public float viscosity = 0.08f;
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [Range(0f, 1f)]
    public float damping = 0.01f;
    [Min(0.0001f)]
    public float timestep = 0.004f;
    [Range(1, 8)]
    public int substeps = 2;
    [Min(0.01f)]
    public float maxVelocity = 8f;

    [Header("Box")]
    public Vector3 boundsSize = new Vector3(4f, 2f, 2f);
    [Range(0f, 1f)]
    public float collisionDamping = 0.45f;
    [Range(0f, 1f)]
    public float tangentDamping = 0.08f;
    [Min(0f)]
    public float wallOffset = 0.035f;

    [Header("Grid")]
    [Min(1)]
    public int maxParticlesPerCell = 128;
    [Min(0.01f)]
    public float cellSizeMultiplier = 1f;

    [Header("Rendering")]
    [Min(1)]
    public int renderStride = 1;

    public int ClampedParticleCount
    {
        get { return Mathf.Clamp(particleCount, 1, Mathf.Max(1, maxParticleCount)); }
    }

    public int ClampedRenderStride
    {
        get { return Mathf.Max(1, renderStride); }
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        modeLabel = profile.mode.ToString();
        particleCount = Mathf.Clamp(profile.targetParticleCount, 1, maxParticleCount);
        renderStride = Mathf.Max(1, profile.renderStride);

        switch (profile.mode)
        {
            case SimulationMode.Debug:
                substeps = 2;
                timestep = 0.004f;
                break;
            case SimulationMode.Presentation:
                substeps = 2;
                timestep = 0.0035f;
                break;
            case SimulationMode.ProfessorBenchmark:
                substeps = 1;
                timestep = 0.003f;
                break;
            case SimulationMode.VRSafe:
                particleCount = Mathf.Clamp(particleCount, 50000, 150000);
                substeps = 1;
                timestep = 0.004f;
                break;
        }
    }

    public void ClampValues()
    {
        maxParticleCount = Mathf.Max(1, maxParticleCount);
        particleCount = ClampedParticleCount;
        particleRadius = Mathf.Max(0.001f, particleRadius);
        smoothingLength = Mathf.Max(particleRadius * 2f, smoothingLength);
        particleMass = Mathf.Max(0.000001f, particleMass);
        restDensity = Mathf.Max(0.001f, restDensity);
        stiffness = Mathf.Max(0f, stiffness);
        viscosity = Mathf.Max(0f, viscosity);
        timestep = Mathf.Max(0.0001f, timestep);
        substeps = Mathf.Clamp(substeps, 1, 8);
        maxVelocity = Mathf.Max(0.01f, maxVelocity);
        boundsSize = new Vector3(
            Mathf.Max(0.1f, boundsSize.x),
            Mathf.Max(0.1f, boundsSize.y),
            Mathf.Max(0.1f, boundsSize.z)
        );
        renderStride = ClampedRenderStride;
        maxParticlesPerCell = Mathf.Max(1, maxParticlesPerCell);
        cellSizeMultiplier = Mathf.Max(0.01f, cellSizeMultiplier);
    }
}
