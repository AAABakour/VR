using UnityEngine;

public class BucketSphSubsystemAdapter : MonoBehaviour, ISimulationSubsystem
{
    public BucketSphFluidController controller;

    public string SubsystemName
    {
        get { return "Bucket SPH Fluid"; }
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<BucketSphFluidController>();
        }
    }

    public void ResetSubsystem()
    {
        if (controller != null)
        {
            controller.ResetBucketSph();
        }
    }

    public void SetPaused(bool paused)
    {
        if (controller != null)
        {
            controller.SetPaused(paused);
        }
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        if (controller != null)
        {
            controller.ApplyProfile(profile);
        }
    }
}
