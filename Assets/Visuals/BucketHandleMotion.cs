using UnityEngine;

public class BucketHandleMotion : MonoBehaviour
{
    [Header("References")]
    public PendulumController pendulumController;
    public Transform bucketRoot;

    [Header("Motion Settings")]
    public float swingRotationStrength = 7f;
    public float maxRotationAngle = 18f;
    public float smoothTime = 0.08f;

    private float currentAngle;
    private float angleVelocity;

    private void LateUpdate()
    {
        if (pendulumController == null || bucketRoot == null)
        {
            return;
        }

        Vector3 worldVelocity = pendulumController.GetBucketVelocity();
        Vector3 localVelocity = bucketRoot.InverseTransformDirection(worldVelocity);

        float targetAngle = -localVelocity.x * swingRotationStrength;
        targetAngle = Mathf.Clamp(targetAngle, -maxRotationAngle, maxRotationAngle);

        currentAngle = Mathf.SmoothDamp(
            currentAngle,
            targetAngle,
            ref angleVelocity,
            smoothTime
        );

        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }
}