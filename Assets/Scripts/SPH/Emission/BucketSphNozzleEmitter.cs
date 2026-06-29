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

    public Vector3 NozzleWorldPosition
    {
        get
        {
            if (nozzlePoint != null)
            {
                return nozzlePoint.position;
            }

            if (collisionProvider != null)
            {
                return collisionProvider.BucketRoot.TransformPoint(collisionProvider.NozzleLocalPosition);
            }

            return transform.position;
        }
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

        Vector3 worldPosition = NozzleWorldPosition;
        Vector3 worldDirection = nozzlePoint != null ? -nozzlePoint.up : Vector3.down;
        Vector3 worldVelocity = bucketMotionDataProvider != null
            ? bucketMotionDataProvider.WorldVelocity * inheritedBucketVelocity
            : Vector3.zero;

        Vector3 simulationPosition = solver.WorldToSimulationPosition(worldPosition);
        Vector3 simulationDirection = solver.WorldToSimulationDirection(worldDirection);
        Vector3 simulationVelocity = solver.WorldToSimulationVelocity(worldVelocity);

        solver.EmitFromNozzle(
            simulationPosition,
            simulationDirection,
            simulationVelocity,
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
