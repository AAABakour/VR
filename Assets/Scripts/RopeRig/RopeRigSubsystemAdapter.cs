using UnityEngine;

public class RopeRigSubsystemAdapter : MonoBehaviour, ISimulationSubsystem
{
    public RopeRigController ropeRigController;
    public bool autoFindReference = true;

    private bool wasEnabled = true;

    public string SubsystemName
    {
        get { return "Rope Rig"; }
    }

    private void Awake()
    {
        ResolveReference();
        if (ropeRigController != null)
        {
            wasEnabled = ropeRigController.enabled;
        }
    }

    public void ResetSubsystem()
    {
        ResolveReference();
        if (ropeRigController != null)
        {
            ropeRigController.RegenerateSegments();
        }
    }

    public void SetPaused(bool paused)
    {
        ResolveReference();
        if (ropeRigController == null)
        {
            return;
        }

        if (paused)
        {
            wasEnabled = ropeRigController.enabled;
            ropeRigController.enabled = false;
            return;
        }

        ropeRigController.enabled = wasEnabled;
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        ResolveReference();
        if (ropeRigController == null || profile == null)
        {
            return;
        }

        ropeRigController.enabled = profile.enableRopeRig;
    }

    private void ResolveReference()
    {
        if (!autoFindReference || ropeRigController != null)
        {
            return;
        }

        ropeRigController = GetComponentInChildren<RopeRigController>(true);
        if (ropeRigController == null)
        {
            ropeRigController = Object.FindFirstObjectByType<RopeRigController>();
        }
    }
}
