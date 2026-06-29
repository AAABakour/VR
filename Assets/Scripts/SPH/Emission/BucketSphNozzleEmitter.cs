using UnityEngine;

[DefaultExecutionOrder(95)]
public class BucketSphNozzleEmitter : MonoBehaviour
{
    public GpuSphSolver solver;
    public BucketSphCollisionProvider collisionProvider;
    public BucketMotionDataProvider bucketMotionDataProvider;
    public Transform nozzlePoint;

    [Header("Emission")]
    public bool emitOnUpdate = false;
    [Min(0f)]
    public float particlesPerSecond = 2400f;
    [Min(1)]
    public int maxParticlesPerFrame = 2048;
    [Min(0.001f)]
    public float nozzleRadius = 0.04f;
    [Min(0f)]
    public float emissionSpeed = 2.4f;
    [Min(0f)]
    public float velocitySpread = 0.45f;
    [Range(0f, 1f)]
    public float inheritedBucketVelocity = 0.55f;
    public bool allowNozzleExit = true;

    private float emissionAccumulator;
    private int emittedThisFrame;

    public int EmittedThisFrame
    {
        get { return emittedThisFrame; }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        emittedThisFrame = 0;
        if (!emitOnUpdate || solver == null || Time.deltaTime <= 0f)
        {
            return;
        }

        emissionAccumulator += particlesPerSecond * Time.deltaTime;
        int emitCount = Mathf.Min(Mathf.FloorToInt(emissionAccumulator), Mathf.Max(1, maxParticlesPerFrame));

        if (emitCount <= 0)
        {
            return;
        }

        emissionAccumulator -= emitCount;
        Emit(emitCount);
    }

    [ContextMenu("Resolve Nozzle Emitter References")]
    public void ResolveReferences()
    {
        if (solver == null)
        {
            solver = Object.FindFirstObjectByType<GpuSphSolver>();
        }

        if (collisionProvider == null)
        {
            collisionProvider = Object.FindFirstObjectByType<BucketSphCollisionProvider>();
        }

        if (bucketMotionDataProvider == null)
        {
            bucketMotionDataProvider = Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }

        if (nozzlePoint == null && collisionProvider != null)
        {
            nozzlePoint = collisionProvider.nozzlePoint;
        }
    }

    public void Emit(int emitCount)
    {
        if (solver == null)
        {
            return;
        }

        BucketSphCollisionProvider provider = collisionProvider;
        Vector3 localPosition = provider != null ? provider.NozzleLocalPosition : Vector3.zero;
        Vector3 localDirection = Vector3.down;
        Vector3 localVelocity = Vector3.zero;

        if (provider != null)
        {
            Transform root = provider.BucketRoot;
            if (nozzlePoint != null)
            {
                localPosition = root.InverseTransformPoint(nozzlePoint.position);
                localDirection = root.InverseTransformDirection(-nozzlePoint.up);
            }

            localVelocity = provider.LocalBucketVelocity * inheritedBucketVelocity;
        }
        else if (nozzlePoint != null)
        {
            localPosition = nozzlePoint.localPosition;
            localDirection = -nozzlePoint.up;
        }

        solver.EmitFromNozzle(
            localPosition,
            localDirection,
            localVelocity,
            emitCount,
            nozzleRadius,
            emissionSpeed,
            velocitySpread,
            allowNozzleExit
        );

        emittedThisFrame = emitCount;
    }

    public void ResetEmitter()
    {
        emissionAccumulator = 0f;
        emittedThisFrame = 0;
    }
}
