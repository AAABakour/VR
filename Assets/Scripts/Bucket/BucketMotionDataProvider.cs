using UnityEngine;

[DefaultExecutionOrder(50)]
public class BucketMotionDataProvider : MonoBehaviour
{
    [Header("Sources")]
    public Transform bucketTransform;
    public PendulumController pendulumController;

    [Header("Smoothing")]
    public bool smoothVelocity = true;
    [Min(0.001f)]
    public float velocitySmoothTime = 0.08f;
    [Min(0.001f)]
    public float angularVelocitySmoothTime = 0.08f;

    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Vector3 smoothedVelocity;
    private Vector3 smoothedAngularVelocity;
    private Vector3 acceleration;
    private Vector3 previousVelocity;
    private bool hasPreviousSample;

    public Transform BucketTransform
    {
        get { return bucketTransform != null ? bucketTransform : transform; }
    }

    public Vector3 WorldPosition
    {
        get { return BucketTransform.position; }
    }

    public Vector3 WorldVelocity
    {
        get { return smoothedVelocity; }
    }

    public Vector3 LocalVelocity
    {
        get { return BucketTransform.InverseTransformDirection(smoothedVelocity); }
    }

    public Vector3 ApproximateAngularVelocity
    {
        get { return smoothedAngularVelocity; }
    }

    public Vector3 Acceleration
    {
        get { return acceleration; }
    }

    private void Awake()
    {
        if (bucketTransform == null)
        {
            bucketTransform = transform;
        }

        ResetProvider();
    }

    private void LateUpdate()
    {
        Sample(Time.deltaTime);
    }

    public void ResetProvider()
    {
        Transform source = BucketTransform;
        previousPosition = source.position;
        previousRotation = source.rotation;
        smoothedVelocity = Vector3.zero;
        smoothedAngularVelocity = Vector3.zero;
        previousVelocity = Vector3.zero;
        acceleration = Vector3.zero;
        hasPreviousSample = true;
    }

    private void Sample(float deltaTime)
    {
        Transform source = BucketTransform;

        if (!hasPreviousSample || deltaTime <= 0f)
        {
            ResetProvider();
            return;
        }

        Vector3 rawVelocity = pendulumController != null
            ? pendulumController.GetBucketVelocity()
            : (source.position - previousPosition) / deltaTime;

        Quaternion deltaRotation = source.rotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
        {
            angle -= 360f;
        }

        Vector3 rawAngularVelocity = axis.sqrMagnitude > 0.000001f
            ? axis.normalized * (angle * Mathf.Deg2Rad / deltaTime)
            : Vector3.zero;

        float velocityBlend = smoothVelocity
            ? 1f - Mathf.Exp(-deltaTime / Mathf.Max(velocitySmoothTime, 0.001f))
            : 1f;

        float angularBlend = smoothVelocity
            ? 1f - Mathf.Exp(-deltaTime / Mathf.Max(angularVelocitySmoothTime, 0.001f))
            : 1f;

        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, velocityBlend);
        smoothedAngularVelocity = Vector3.Lerp(smoothedAngularVelocity, rawAngularVelocity, angularBlend);
        acceleration = (smoothedVelocity - previousVelocity) / deltaTime;

        previousVelocity = smoothedVelocity;
        previousPosition = source.position;
        previousRotation = source.rotation;
    }
}
