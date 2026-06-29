using UnityEngine;

public class LegacyPaintSystemAdapter : MonoBehaviour, ISimulationSubsystem
{
    [Header("Legacy Paint References")]
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;

    [Header("Discovery")]
    public bool autoFindReferences = true;

    public string SubsystemName
    {
        get { return "Legacy Paint System"; }
    }

    private void Awake()
    {
        if (autoFindReferences)
        {
            ResolveReferences();
        }
    }

    public void ResetSubsystem()
    {
        if (canvasPainter != null)
        {
            canvasPainter.ResetCanvas();
        }

        if (paintEmitter != null)
        {
            paintEmitter.ResetEmitter();
        }
        else if (particleSimulator != null)
        {
            particleSimulator.ResetParticles();
        }
    }

    public void SetPaused(bool paused)
    {
        if (paintEmitter != null)
        {
            paintEmitter.enabled = !paused;
        }

        if (particleSimulator != null)
        {
            particleSimulator.enabled = !paused;
        }
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        SetLegacyPaintEnabled(profile.enableLegacyParticleSystem);
    }

    [ContextMenu("Resolve References")]
    public void ResolveReferences()
    {
        if (paintEmitter == null)
        {
            paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (particleSimulator == null)
        {
            particleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        }

        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }
    }

    private void SetLegacyPaintEnabled(bool enabled)
    {
        if (paintEmitter != null)
        {
            paintEmitter.enabled = enabled;
        }

        if (particleSimulator != null)
        {
            particleSimulator.enabled = enabled;
        }
    }
}
