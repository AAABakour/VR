using UnityEngine;

public class LegacyPendulumAdapter : MonoBehaviour, ISimulationSubsystem
{
    [Header("Legacy Pendulum Reference")]
    public PendulumController pendulumController;

    [Header("Discovery")]
    public bool autoFindReference = true;

    public string SubsystemName
    {
        get { return "Legacy Pendulum"; }
    }

    public Vector3 BucketVelocity
    {
        get
        {
            return pendulumController != null
                ? pendulumController.GetBucketVelocity()
                : Vector3.zero;
        }
    }

    private void Awake()
    {
        if (autoFindReference)
        {
            ResolveReference();
        }
    }

    public void ResetSubsystem()
    {
        if (pendulumController != null)
        {
            pendulumController.ResetSimulation();
        }
    }

    public void SetPaused(bool paused)
    {
        if (pendulumController != null)
        {
            pendulumController.enabled = !paused;
        }
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        // Future rope rigs can replace this adapter without changing the lifecycle API.
    }

    [ContextMenu("Resolve Reference")]
    public void ResolveReference()
    {
        if (pendulumController == null)
        {
            pendulumController = Object.FindFirstObjectByType<PendulumController>();
        }
    }
}
