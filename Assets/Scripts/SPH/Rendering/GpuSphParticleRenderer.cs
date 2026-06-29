using UnityEngine;

[DefaultExecutionOrder(120)]
public class GpuSphParticleRenderer : MonoBehaviour
{
    public GpuSphSolver solver;
    public FluidBoxController fluidBox;
    public Material particleMaterial;
    public Camera renderCamera;
    public float particleSize = 0.045f;
    public Color particleColor = new Color(0.18f, 0.65f, 1f, 0.75f);

    private ComputeBuffer argsBuffer;
    private int cachedRenderedCount = -1;
    private int cachedStride = -1;
    private Material runtimeMaterial;
    private bool warnedMissingMaterial;
    private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    private static readonly int RenderStrideId = Shader.PropertyToID("_RenderStride");
    private static readonly int ParticleSizeId = Shader.PropertyToID("_ParticleSize");
    private static readonly int ParticleColorId = Shader.PropertyToID("_ParticleColor");
    private static readonly int LocalToWorldId = Shader.PropertyToID("_BoxLocalToWorld");
    private static readonly int CameraRightId = Shader.PropertyToID("_CameraRight");
    private static readonly int CameraUpId = Shader.PropertyToID("_CameraUp");

    private void LateUpdate()
    {
        DrawParticles();
    }

    private void OnDisable()
    {
        ReleaseArgs();
    }

    private void OnDestroy()
    {
        ReleaseArgs();
    }

    public void DrawParticles()
    {
        if (solver == null || solver.ParticleBuffer == null)
        {
            return;
        }

        Material material = GetMaterial();
        if (material == null)
        {
            return;
        }

        int renderedCount = solver.RenderedParticleCount;
        int stride = solver.RenderStride;
        EnsureArgsBuffer(renderedCount, stride);

        Camera camera = renderCamera != null ? renderCamera : Camera.main;
        Vector3 cameraRight = camera != null ? camera.transform.right : Vector3.right;
        Vector3 cameraUp = camera != null ? camera.transform.up : Vector3.up;
        Matrix4x4 localToWorld = fluidBox != null ? fluidBox.LocalToWorldMatrix : solver.SimulationLocalToWorldMatrix;
        Vector3 boundsSize = fluidBox != null ? fluidBox.BoundsSize : solver.SimulationBoundsSize;
        Vector3 center = fluidBox != null ? fluidBox.transform.position : solver.SimulationWorldCenter;
        Bounds bounds = new Bounds(center, boundsSize + Vector3.one * 2f);

        material.SetBuffer(ParticlesId, solver.ParticleBuffer);
        material.SetInt(RenderStrideId, stride);
        material.SetFloat(ParticleSizeId, particleSize);
        material.SetColor(ParticleColorId, particleColor);
        material.SetMatrix(LocalToWorldId, localToWorld);
        material.SetVector(CameraRightId, cameraRight);
        material.SetVector(CameraUpId, cameraUp);

        Graphics.DrawProceduralIndirect(material, bounds, MeshTopology.Triangles, argsBuffer);
    }

    private Material GetMaterial()
    {
        if (particleMaterial != null)
        {
            return particleMaterial;
        }

        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        if (!warnedMissingMaterial)
        {
            warnedMissingMaterial = true;
            Debug.LogWarning("[GpuSphParticleRenderer] No explicit particle material assigned; falling back to Shader.Find.", this);
        }

        Shader shader = Shader.Find("SwingingPaintBucket/SPH/GPU Particle Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[GpuSphParticleRenderer] Particle shader was not found.", this);
            return null;
        }

        runtimeMaterial = new Material(shader);
        runtimeMaterial.name = "Runtime GPU SPH Particle Material";
        return runtimeMaterial;
    }

    private void EnsureArgsBuffer(int renderedCount, int stride)
    {
        if (argsBuffer != null && cachedRenderedCount == renderedCount && cachedStride == stride)
        {
            return;
        }

        ReleaseArgs();
        argsBuffer = new ComputeBuffer(4, GpuSphBufferUtility.IndirectArgsStrideBytes, ComputeBufferType.IndirectArguments);
        uint vertexCount = (uint)Mathf.Max(0, renderedCount * 6);
        argsBuffer.SetData(new uint[] { vertexCount, 1, 0, 0 });
        cachedRenderedCount = renderedCount;
        cachedStride = stride;
    }

    private void ReleaseArgs()
    {
        GpuSphBufferUtility.ReleaseBuffer(ref argsBuffer);
        cachedRenderedCount = -1;
        cachedStride = -1;
    }
}
