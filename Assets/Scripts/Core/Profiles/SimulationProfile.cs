using UnityEngine;

[CreateAssetMenu(
    fileName = "SimulationProfile",
    menuName = "Swinging Paint Bucket/Simulation Profile"
)]
public class SimulationProfile : ScriptableObject
{
    [Header("Mode")]
    public SimulationMode mode = SimulationMode.LegacyPrototype;

    [Header("Performance Targets")]
    [Min(0)]
    public int targetParticleCount = 1200;
    [Min(1)]
    public int renderStride = 1;
    [Min(0)]
    public int maxImpactEventsPerFrame = 64;

    [Header("Subsystem Toggles")]
    public bool enableLegacyParticleSystem = true;
    public bool enableGpuSph = false;
    public bool enableRopeRig = false;
    public bool enableDebugVisuals = true;

    [Header("Notes")]
    [TextArea(3, 8)]
    public string description;
}
