using UnityEngine;

[CreateAssetMenu(
    fileName = "PaintSurfaceProfile",
    menuName = "Swinging Paint/Impact V2/Surface Profile"
)]
public class PaintSurfaceProfile : ScriptableObject
{
    [Header("Basic Surface Identity")]
    public string surfaceName = "Canvas";

    [Header("Physical Response")]
    [Range(0f, 1f)]
    public float absorption = 0.35f;

    [Range(0f, 1f)]
    public float roughness = 0.55f;

    [Range(0f, 2f)]
    public float spreadFactor = 1.0f;

    [Range(0f, 2f)]
    public float smearFactor = 1.0f;

    [Range(0f, 2f)]
    public float splashFactor = 1.0f;

    [Range(0f, 1f)]
    public float dripResistance = 0.55f;

    [Range(0f, 2f)]
    public float dryingSpeed = 0.5f;

    [Range(0f, 1f)]
    public float wetFriction = 0.45f;

    [Header("Visual Response")]
    [Range(0f, 2f)]
    public float edgeNoise = 1.0f;

    [Range(0f, 2f)]
    public float microTextureStrength = 1.0f;

    [Range(0f, 1f)]
    public float colorStaining = 0.75f;
}