using UnityEngine;

public class BucketCollisionProxy : MonoBehaviour
{
    [Header("References")]
    public Transform bucketRoot;
    public Transform ropeAttachPoint;

    [Header("Approximate Bucket Shape")]
    public Vector3 localCenter = new Vector3(0f, -0.35f, 0f);
    [Min(0.01f)]
    public float radius = 0.45f;
    [Min(0.01f)]
    public float height = 0.9f;
    public float bottomOffset = -0.45f;
    [Min(0.001f)]
    public float wallThickness = 0.035f;

    [Header("Nozzle")]
    public Vector3 nozzleLocalPosition = new Vector3(-0.14f, -1f, 0f);
    [Min(0.001f)]
    public float nozzleRadius = 0.04f;

    [Header("Debug")]
    public bool showGizmos = true;

    public Transform BucketRoot
    {
        get { return bucketRoot != null ? bucketRoot : transform; }
    }

    public Vector3 WorldCenter
    {
        get { return BucketRoot.TransformPoint(localCenter); }
    }

    public Vector3 WorldNozzlePosition
    {
        get { return BucketRoot.TransformPoint(nozzleLocalPosition); }
    }

    private void Reset()
    {
        bucketRoot = transform;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Transform root = BucketRoot;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = root.localToWorldMatrix;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(localCenter, new Vector3(radius * 2f, height, radius * 2f));

        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.85f);
        Gizmos.DrawWireSphere(localCenter + Vector3.down * Mathf.Abs(bottomOffset), radius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(nozzleLocalPosition, nozzleRadius);

        Gizmos.matrix = previousMatrix;

        if (ropeAttachPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(ropeAttachPoint.position, 0.05f);
        }
    }
}
