using UnityEngine;

public class BucketRigController : MonoBehaviour
{
    [Header("Bucket References")]
    public Transform bucketRoot;
    public Transform bucketBody;
    public Transform handlePivot;
    public Transform handleVisual;
    public Transform ropeAttachPoint;
    public Transform nozzlePoint;

    [Header("Rig Components")]
    public BucketCollisionProxy bucketCollisionProxy;
    public BucketMotionDataProvider motionDataProvider;

    [Header("Discovery")]
    public bool autoFindChildrenByName = true;
    public bool showDebugGizmos = true;

    public Transform BucketRoot
    {
        get { return bucketRoot != null ? bucketRoot : transform; }
    }

    public Transform RopeAttachPoint
    {
        get { return ropeAttachPoint; }
    }

    public Transform NozzlePoint
    {
        get { return nozzlePoint; }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        if (autoFindChildrenByName)
        {
            ResolveReferences();
        }
    }

    [ContextMenu("Resolve Bucket References")]
    public void ResolveReferences()
    {
        if (bucketRoot == null)
        {
            bucketRoot = transform;
        }

        if (!autoFindChildrenByName)
        {
            ResolveComponentsOnly();
            return;
        }

        if (bucketBody == null)
        {
            bucketBody = FindChildByName(bucketRoot, "Bucket_Body");
        }

        if (handlePivot == null)
        {
            handlePivot = FindChildByName(bucketRoot, "HandlePivot");
        }

        if (handleVisual == null)
        {
            handleVisual = FindChildByName(bucketRoot, "Bucket_Hand");
        }

        if (ropeAttachPoint == null)
        {
            ropeAttachPoint = FindChildByName(bucketRoot, "RopeAttachPoint");
        }

        if (nozzlePoint == null)
        {
            nozzlePoint = FindChildByName(bucketRoot, "PaintNozzle");
        }

        ResolveComponentsOnly();
    }

    public bool ValidateReferences(bool logWarnings)
    {
        bool valid = true;

        valid &= ValidateReference(bucketRoot, "bucketRoot", logWarnings);
        valid &= ValidateReference(ropeAttachPoint, "ropeAttachPoint", logWarnings);
        valid &= ValidateReference(nozzlePoint, "nozzlePoint", logWarnings);
        valid &= ValidateReference(motionDataProvider, "motionDataProvider", logWarnings);
        valid &= ValidateReference(bucketCollisionProxy, "bucketCollisionProxy", logWarnings);

        return valid;
    }

    private void ResolveComponentsOnly()
    {
        if (motionDataProvider == null)
        {
            motionDataProvider = GetComponent<BucketMotionDataProvider>();
        }

        if (bucketCollisionProxy == null)
        {
            bucketCollisionProxy = GetComponent<BucketCollisionProxy>();
        }
    }

    private bool ValidateReference(Object reference, string label, bool logWarnings)
    {
        if (reference != null)
        {
            return true;
        }

        if (logWarnings)
        {
            Debug.LogWarning("[BucketRigController] Missing " + label + ".", this);
        }

        return false;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        if (ropeAttachPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(ropeAttachPoint.position, 0.05f);
        }

        if (nozzlePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(nozzlePoint.position, 0.04f);
        }

        if (handlePivot != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(handlePivot.position, handlePivot.position + handlePivot.TransformDirection(Vector3.right) * 0.35f);
        }
    }
}
