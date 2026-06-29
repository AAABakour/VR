using UnityEngine;

public class BucketRigSubsystemAdapter : MonoBehaviour, ISimulationSubsystem
{
    public BucketRigController bucketRigController;
    public BucketHandleRig bucketHandleRig;
    public BucketMotionDataProvider motionDataProvider;
    public BucketCollisionProxy collisionProxy;
    public bool autoFindReferences = true;

    private bool handleMotionWasEnabled = true;

    public string SubsystemName
    {
        get { return "Bucket Rig"; }
    }

    private void Awake()
    {
        ResolveReferences();
        if (bucketHandleRig != null)
        {
            handleMotionWasEnabled = bucketHandleRig.enableMotion;
        }
    }

    public void ResetSubsystem()
    {
        ResolveReferences();

        if (motionDataProvider != null)
        {
            motionDataProvider.ResetProvider();
        }

        if (bucketHandleRig != null)
        {
            bucketHandleRig.ResetHandle();
        }
    }

    public void SetPaused(bool paused)
    {
        ResolveReferences();

        if (bucketHandleRig == null)
        {
            return;
        }

        if (paused)
        {
            handleMotionWasEnabled = bucketHandleRig.enableMotion;
            bucketHandleRig.enableMotion = false;
            return;
        }

        bucketHandleRig.enableMotion = handleMotionWasEnabled;
    }

    public void ApplySimulationProfile(SimulationProfile profile)
    {
        ResolveReferences();

        if (profile == null)
        {
            return;
        }

        bool enableRig = profile.enableRopeRig;

        if (bucketRigController != null)
        {
            bucketRigController.enabled = enableRig;
        }

        if (bucketHandleRig != null)
        {
            bucketHandleRig.enabled = enableRig;
        }

        if (motionDataProvider != null)
        {
            motionDataProvider.enabled = enableRig;
        }

        if (collisionProxy != null)
        {
            collisionProxy.enabled = enableRig;
        }
    }

    private void ResolveReferences()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (bucketRigController == null)
        {
            bucketRigController = GetComponentInChildren<BucketRigController>(true);
        }

        if (bucketHandleRig == null)
        {
            bucketHandleRig = GetComponentInChildren<BucketHandleRig>(true);
        }

        if (motionDataProvider == null)
        {
            motionDataProvider = GetComponentInChildren<BucketMotionDataProvider>(true);
        }

        if (collisionProxy == null)
        {
            collisionProxy = GetComponentInChildren<BucketCollisionProxy>(true);
        }

        if (bucketRigController == null)
        {
            bucketRigController = Object.FindFirstObjectByType<BucketRigController>();
        }

        if (bucketHandleRig == null)
        {
            bucketHandleRig = Object.FindFirstObjectByType<BucketHandleRig>();
        }

        if (motionDataProvider == null)
        {
            motionDataProvider = Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }

        if (collisionProxy == null)
        {
            collisionProxy = Object.FindFirstObjectByType<BucketCollisionProxy>();
        }
    }
}
