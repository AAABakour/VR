using UnityEngine;

[DefaultExecutionOrder(55)]
public class BucketSphCollisionProvider : MonoBehaviour
{
    [Header("Bucket Sources")]
    public BucketRigController bucketRigController;
    public BucketCollisionProxy bucketCollisionProxy;
    public BucketMotionDataProvider motionDataProvider;
    public Transform bucketRoot;
    public Transform nozzlePoint;

    [Header("SPH Safety Bounds")]
    public Vector3 externalBoundsSize = new Vector3(4.5f, 4.5f, 4.5f);
    public bool allowNozzleExit = true;

    [Header("Debug")]
    public bool autoResolveOnAwake = true;
    public bool showGizmos = true;

    public Transform BucketRoot
    {
        get
        {
            if (bucketRoot != null)
            {
                return bucketRoot;
            }

            if (bucketCollisionProxy != null)
            {
                return bucketCollisionProxy.BucketRoot;
            }

            if (bucketRigController != null)
            {
                return bucketRigController.BucketRoot;
            }

            return transform;
        }
    }

    public Matrix4x4 LocalToWorldMatrix
    {
        get { return BucketRoot.localToWorldMatrix; }
    }

    public Vector3 ExternalBoundsSize
    {
        get
        {
            return new Vector3(
                Mathf.Max(0.1f, externalBoundsSize.x),
                Mathf.Max(0.1f, externalBoundsSize.y),
                Mathf.Max(0.1f, externalBoundsSize.z)
            );
        }
    }

    public Vector3 LocalGravity
    {
        get
        {
            Vector3 inertialGravity = Physics.gravity;
            if (motionDataProvider != null)
            {
                inertialGravity -= motionDataProvider.Acceleration;
            }

            return BucketRoot.InverseTransformDirection(inertialGravity);
        }
    }

    public Vector3 LocalBucketVelocity
    {
        get
        {
            return motionDataProvider != null
                ? BucketRoot.InverseTransformDirection(motionDataProvider.WorldVelocity)
                : Vector3.zero;
        }
    }

    public Vector3 LocalCenter
    {
        get { return bucketCollisionProxy != null ? bucketCollisionProxy.localCenter : new Vector3(0f, -0.35f, 0f); }
    }

    public float Radius
    {
        get { return bucketCollisionProxy != null ? bucketCollisionProxy.radius : 0.45f; }
    }

    public float Height
    {
        get { return bucketCollisionProxy != null ? bucketCollisionProxy.height : 0.9f; }
    }

    public float BottomOffset
    {
        get { return bucketCollisionProxy != null ? bucketCollisionProxy.bottomOffset : -0.45f; }
    }

    public float WallThickness
    {
        get { return bucketCollisionProxy != null ? bucketCollisionProxy.wallThickness : 0.035f; }
    }

    public Vector3 NozzleLocalPosition
    {
        get
        {
            if (bucketCollisionProxy != null)
            {
                return bucketCollisionProxy.LocalNozzlePosition;
            }

            return nozzlePoint != null
                ? BucketRoot.InverseTransformPoint(nozzlePoint.position)
                : new Vector3(-0.14f, -1f, 0f);
        }
    }

    public float NozzleRadius
    {
        get { return bucketCollisionProxy != null ? bucketCollisionProxy.nozzleRadius : 0.04f; }
    }

    private void Awake()
    {
        if (autoResolveOnAwake)
        {
            ResolveReferences();
        }
    }

    private void OnValidate()
    {
        externalBoundsSize = ExternalBoundsSize;
    }

    [ContextMenu("Resolve Bucket SPH References")]
    public void ResolveReferences()
    {
        if (bucketRigController == null)
        {
            bucketRigController = Object.FindFirstObjectByType<BucketRigController>();
        }

        if (bucketCollisionProxy == null)
        {
            bucketCollisionProxy = bucketRigController != null
                ? bucketRigController.bucketCollisionProxy
                : Object.FindFirstObjectByType<BucketCollisionProxy>();
        }

        if (motionDataProvider == null)
        {
            motionDataProvider = bucketRigController != null
                ? bucketRigController.motionDataProvider
                : Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }

        if (bucketRoot == null)
        {
            if (bucketRigController != null)
            {
                bucketRoot = bucketRigController.BucketRoot;
            }
            else if (bucketCollisionProxy != null)
            {
                bucketRoot = bucketCollisionProxy.BucketRoot;
            }
        }

        if (nozzlePoint == null)
        {
            if (bucketRigController != null)
            {
                nozzlePoint = bucketRigController.NozzlePoint;
            }
            else if (bucketCollisionProxy != null)
            {
                nozzlePoint = bucketCollisionProxy.nozzlePoint;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Transform root = BucketRoot;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = root.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, ExternalBoundsSize);
        Gizmos.color = new Color(0f, 0.55f, 1f, 0.75f);
        Gizmos.DrawWireCube(LocalCenter, new Vector3(Radius * 2f, Height, Radius * 2f));
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(NozzleLocalPosition, NozzleRadius);
        Gizmos.matrix = previous;
    }
}
