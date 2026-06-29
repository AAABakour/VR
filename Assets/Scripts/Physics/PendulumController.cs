using UnityEngine;

public class PendulumController : MonoBehaviour
{
    [Header("Scene References")]
    public Transform pivotPoint;
    public Transform bucket;
    public Transform rope;
    public Transform ropeAttachPoint;

    [Header("Main Pendulum Settings")]
    public float ropeLength = 2.2f;
    public float startAngleDegrees = 35f;
    public float initialAngularVelocity = 0f;
    public float gravity = 9.81f;
    public float damping = 0.05f;

    [Header("Secondary Direction Settings")]
    public bool enableDepthSwing = true;
    public float startAngleZDegrees = 18f;
    public float initialAngularVelocityZ = 0.8f;
    public float zSwingStrength = 0.6f;

    [Header("Rope Realism")]
    public bool enableRopeStretch = true;
    [Range(0f, 0.25f)]
    public float ropeElasticity = 0.035f;
    public float maxRopeStretch = 0.18f;

    [Header("Bucket Wobble")]
    public bool enableBucketWobble = true;
    public float wobbleStrength = 8f;
    public float wobbleDamping = 2.2f;

    [Header("Bucket Torsion")]
    public bool enableBucketTorsion = true;
    public float torsionStrength = 18f;
    public float torsionDamping = 1.8f;

    [Header("Visual Settings")]
    public bool updateLegacyRopeVisual = true;
    public float ropeThickness = 0.03f;

    private float angleX;
    private float angularVelocityX;

    private float angleZ;
    private float angularVelocityZ;

    private float visualRopeLength;

    private Vector3 previousBucketPosition;
    private Vector3 bucketVelocity;

    private float wobbleX;
    private float wobbleZ;
    private float wobbleVelocityX;
    private float wobbleVelocityZ;

    private float torsionAngle;
    private float torsionVelocity;

    void Start()
    {
        ResetSimulation();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        SimulatePendulum(dt);
        SimulateBucketSecondaryMotion(dt);
        UpdateVisuals(dt);
    }

    public void ResetSimulation()
    {
        angleX = startAngleDegrees * Mathf.Deg2Rad;
        angularVelocityX = initialAngularVelocity;

        angleZ = startAngleZDegrees * Mathf.Deg2Rad;
        angularVelocityZ = initialAngularVelocityZ;

        visualRopeLength = ropeLength;

        wobbleX = 0f;
        wobbleZ = 0f;
        wobbleVelocityX = 0f;
        wobbleVelocityZ = 0f;

        torsionAngle = 0f;
        torsionVelocity = 0f;

        if (bucket != null)
        {
            previousBucketPosition = bucket.position;
        }

        UpdateVisuals(0f);
    }

    private void SimulatePendulum(float deltaTime)
    {
        SimulateSingleAxis(ref angleX, ref angularVelocityX, deltaTime);

        if (enableDepthSwing)
        {
            SimulateSingleAxis(ref angleZ, ref angularVelocityZ, deltaTime);
        }
    }

    private void SimulateSingleAxis(ref float angle, ref float angularVelocity, float deltaTime)
    {
        float angularAcceleration =
            -(gravity / ropeLength) * Mathf.Sin(angle)
            - damping * angularVelocity;

        angularVelocity += angularAcceleration * deltaTime;
        angle += angularVelocity * deltaTime;
    }

    private void SimulateBucketSecondaryMotion(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        if (enableBucketWobble)
        {
            float targetWobbleX = -angularVelocityZ * wobbleStrength;
            float targetWobbleZ = angularVelocityX * wobbleStrength;

            wobbleX = Mathf.SmoothDamp(
                wobbleX,
                targetWobbleX,
                ref wobbleVelocityX,
                1f / Mathf.Max(wobbleDamping, 0.01f),
                Mathf.Infinity,
                dt
            );

            wobbleZ = Mathf.SmoothDamp(
                wobbleZ,
                targetWobbleZ,
                ref wobbleVelocityZ,
                1f / Mathf.Max(wobbleDamping, 0.01f),
                Mathf.Infinity,
                dt
            );
        }
        else
        {
            wobbleX = 0f;
            wobbleZ = 0f;
        }

        if (enableBucketTorsion)
        {
            float targetTorsion = (angularVelocityX - angularVelocityZ) * torsionStrength;

            torsionAngle = Mathf.SmoothDamp(
                torsionAngle,
                targetTorsion,
                ref torsionVelocity,
                1f / Mathf.Max(torsionDamping, 0.01f),
                Mathf.Infinity,
                dt
            );
        }
        else
        {
            torsionAngle = 0f;
        }
    }

    private float GetEffectiveRopeLength()
    {
        if (!enableRopeStretch)
        {
            return ropeLength;
        }

        float swingSpeed =
            Mathf.Abs(angularVelocityX) +
            Mathf.Abs(angularVelocityZ) * zSwingStrength;

        float stretch = swingSpeed * swingSpeed * ropeElasticity;
        stretch = Mathf.Clamp(stretch, 0f, maxRopeStretch);

        visualRopeLength = ropeLength + stretch;

        return visualRopeLength;
    }

    private void UpdateVisuals(float dt)
    {
        if (pivotPoint == null || bucket == null)
        {
            return;
        }

        Vector3 pivotPosition = pivotPoint.position;

        float effectiveLength = GetEffectiveRopeLength();

        float x = Mathf.Sin(angleX) * effectiveLength;

        float z = 0f;

        if (enableDepthSwing)
        {
            z = Mathf.Sin(angleZ) * effectiveLength * zSwingStrength;
        }

        Vector2 horizontalOffset = new Vector2(x, z);
        float maxHorizontalDistance = effectiveLength * 0.95f;

        if (horizontalOffset.magnitude > maxHorizontalDistance)
        {
            horizontalOffset = horizontalOffset.normalized * maxHorizontalDistance;
            x = horizontalOffset.x;
            z = horizontalOffset.y;
        }

        float verticalDistance = Mathf.Sqrt(
            Mathf.Max((effectiveLength * effectiveLength) - (x * x) - (z * z), 0.01f)
        );

        Vector3 bucketPosition = pivotPosition + new Vector3(
            x,
            -verticalDistance,
            z
        );

        if (dt > 0f)
        {
            bucketVelocity = (bucketPosition - previousBucketPosition) / dt;
        }

        previousBucketPosition = bucketPosition;

        bucket.position = bucketPosition;

        UpdateBucketRotation();

        Vector3 ropeEndPosition = ropeAttachPoint != null
            ? ropeAttachPoint.position
            : bucketPosition;

        if (updateLegacyRopeVisual && rope != null)
        {
            UpdateRopeVisual(pivotPosition, ropeEndPosition);
        }
    }

    private void UpdateBucketRotation()
    {
        Quaternion wobbleRotation = Quaternion.Euler(
            Mathf.Clamp(wobbleX, -18f, 18f),
            torsionAngle,
            Mathf.Clamp(wobbleZ, -18f, 18f)
        );

        bucket.rotation = wobbleRotation;
    }

    private void LateUpdate()
    {
        if (!updateLegacyRopeVisual || pivotPoint == null || rope == null || bucket == null)
        {
            return;
        }

        Vector3 ropeEndPosition = ropeAttachPoint != null
            ? ropeAttachPoint.position
            : bucket.position;

        UpdateRopeVisual(pivotPoint.position, ropeEndPosition);
    }

    private void UpdateRopeVisual(Vector3 pivotPosition, Vector3 bucketPosition)
    {
        Vector3 ropeDirection = bucketPosition - pivotPosition;
        Vector3 ropeCenter = pivotPosition + ropeDirection * 0.5f;

        rope.position = ropeCenter;
        rope.up = ropeDirection.normalized;

        rope.localScale = new Vector3(
            ropeThickness,
            ropeDirection.magnitude / 2f,
            ropeThickness
        );
    }

    public Vector3 GetBucketVelocity()
    {
        return bucketVelocity;
    }
}
