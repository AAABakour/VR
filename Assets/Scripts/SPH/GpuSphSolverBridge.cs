using UnityEngine;

[DefaultExecutionOrder(20)]
public class GpuSphSolverBridge : MonoBehaviour
{
    private const string RuntimeObjectName = "GpuSphSolverBridge_Runtime";

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GpuSphParticle
    {
        public Vector3 position;
        public float radius;
        public Vector3 velocity;
        public float density;
        public Vector4 color;
        public float pressure;
        public float viscosity;
        public float life;
        public float flags;
    }

    [Header("GPU Solver Assets")]
    public ComputeShader sphCompute;
    public Material fluidRenderMaterial;
    public Mesh renderParticleMesh;

    [Header("References")]
    public PaintEmitter emitter;
    public SphParticleBudgetController budgetController;
    public SphCollisionSurfaceBridge collisionSurfaceBridge;

    [Header("Execution")]
    public bool enableGpuSphRuntime = false;
    public bool allocateBuffersOnEnable = false;
    [Range(1024, 4000000)] public int particleCapacity = 1000000;
    [Range(1, 8)] public int solverSubsteps = 2;
    [Range(0.002f, 0.033f)] public float solverDeltaTime = 0.0083333f;

    [Header("Runtime Readout")]
    [SerializeField] private bool buffersAllocated;
    [SerializeField] private string runtimeStatus = "GPU SPH bridge installed; runtime disabled until solver assets are assigned.";

    private ComputeBuffer particleBuffer;
    private ComputeBuffer argsBuffer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<GpuSphSolverBridge>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<GpuSphSolverBridge>();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        if (allocateBuffersOnEnable && enableGpuSphRuntime)
        {
            AllocateBuffers();
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void Update()
    {
        if (!enableGpuSphRuntime)
        {
            return;
        }

        if (!buffersAllocated)
        {
            AllocateBuffers();
        }

        if (!buffersAllocated || sphCompute == null)
        {
            runtimeStatus = "GPU SPH waiting for compute shader / buffers.";
            return;
        }

        DispatchSolver(Time.deltaTime);
    }

    private void AutoFindReferences()
    {
        if (emitter == null) emitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (budgetController == null) budgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
        if (collisionSurfaceBridge == null) collisionSurfaceBridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
    }

    public void AllocateBuffers()
    {
        ReleaseBuffers();
        AutoFindReferences();

        int capacity = budgetController != null ? budgetController.targetParticleCount : particleCapacity;
        particleCapacity = Mathf.Clamp(capacity, 1024, 4000000);

        particleBuffer = new ComputeBuffer(particleCapacity, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GpuSphParticle)), ComputeBufferType.Structured);
        argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

        uint indexCount = renderParticleMesh != null ? renderParticleMesh.GetIndexCount(0) : 0u;
        uint[] args = { indexCount, (uint)particleCapacity, 0u, 0u, 0u };
        argsBuffer.SetData(args);

        buffersAllocated = true;
        runtimeStatus = "GPU buffers allocated for " + particleCapacity.ToString("N0") + " SPH particles.";
        Debug.Log("[VR SPH] " + runtimeStatus);
    }

    public void ReleaseBuffers()
    {
        if (particleBuffer != null)
        {
            particleBuffer.Release();
            particleBuffer = null;
        }

        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }

        buffersAllocated = false;
    }

    public void DispatchSolver(float frameDt)
    {
        if (sphCompute == null || particleBuffer == null)
        {
            return;
        }

        float dt = Mathf.Min(frameDt, solverDeltaTime);
        int kernel = sphCompute.FindKernel("CSApplyForces");
        sphCompute.SetFloat("_DeltaTime", dt / Mathf.Max(1, solverSubsteps));
        sphCompute.SetInt("_ParticleCount", particleCapacity);
        sphCompute.SetBuffer(kernel, "_Particles", particleBuffer);

        int groups = Mathf.CeilToInt(particleCapacity / 256f);
        for (int i = 0; i < solverSubsteps; i++)
        {
            sphCompute.Dispatch(kernel, groups, 1, 1);
        }

        runtimeStatus = "GPU SPH dispatch OK | capacity " + particleCapacity.ToString("N0") + " | substeps " + solverSubsteps;
    }

    public string RuntimeStatus => runtimeStatus;
    public bool BuffersAllocated => buffersAllocated;
}
