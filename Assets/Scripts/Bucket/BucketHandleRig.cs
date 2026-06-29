using UnityEngine;

[DefaultExecutionOrder(100)]
public class BucketHandleRig : MonoBehaviour
{
    [Header("Motion Sources")]
    public PendulumController pendulumController;
    public BucketMotionDataProvider motionDataProvider;
    public Transform bucketRoot;
    public Transform handlePivot;

    [Header("Handle Motion")]
    public Vector3 localRotationAxis = Vector3.right;
    public float strength = 4f;
    [Range(0f, 45f)]
    public float maxAngle = 14f;
    [Min(0.001f)]
    public float smoothTime = 0.12f;
    public float returnToRestStrength = 1.5f;
    public bool invertDirection = false;
    public bool enableMotion = true;

    private Quaternion restLocalRotation;
    private float currentAngle;
    private float angleVelocity;

    private void Awake()
    {
        if (handlePivot == null)
        {
            handlePivot = transform;
        }

        if (bucketRoot == null && handlePivot.parent != null)
        {
            bucketRoot = handlePivot.root;
        }

        restLocalRotation = handlePivot.localRotation;
    }

    private void OnEnable()
    {
        ResetHandle();
    }

    private void LateUpdate()
    {
        if (handlePivot == null)
        {
            return;
        }

        if (!enableMotion)
        {
            ApplyAngle(0f, Time.deltaTime);
            return;
        }

        Vector3 worldVelocity = GetWorldVelocity();
        Vector3 localVelocity = bucketRoot != null
            ? bucketRoot.InverseTransformDirection(worldVelocity)
            : worldVelocity;

        float sign = invertDirection ? -1f : 1f;
        float targetAngle = -localVelocity.x * strength * sign;
        targetAngle += -currentAngle * returnToRestStrength * Time.deltaTime;
        targetAngle = Mathf.Clamp(targetAngle, -maxAngle, maxAngle);

        ApplyAngle(targetAngle, Time.deltaTime);
    }

    [ContextMenu("Reset Handle")]
    public void ResetHandle()
    {
        if (handlePivot == null)
        {
            return;
        }

        currentAngle = 0f;
        angleVelocity = 0f;
        restLocalRotation = handlePivot.localRotation;
        handlePivot.localRotation = restLocalRotation;
    }

    private Vector3 GetWorldVelocity()
    {
        if (motionDataProvider != null)
        {
            return motionDataProvider.WorldVelocity;
        }

        if (pendulumController != null)
        {
            return pendulumController.GetBucketVelocity();
        }

        return Vector3.zero;
    }

    private void ApplyAngle(float targetAngle, float deltaTime)
    {
        currentAngle = Mathf.SmoothDamp(
            currentAngle,
            targetAngle,
            ref angleVelocity,
            Mathf.Max(smoothTime, 0.001f),
            Mathf.Infinity,
            deltaTime
        );

        Vector3 axis = localRotationAxis.sqrMagnitude > 0.0001f
            ? localRotationAxis.normalized
            : Vector3.right;

        handlePivot.localRotation = restLocalRotation * Quaternion.AngleAxis(currentAngle, axis);
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = handlePivot != null ? handlePivot : transform;
        Vector3 axis = localRotationAxis.sqrMagnitude > 0.0001f
            ? localRotationAxis.normalized
            : Vector3.right;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(pivot.position, pivot.position + pivot.TransformDirection(axis) * 0.35f);
    }
}
