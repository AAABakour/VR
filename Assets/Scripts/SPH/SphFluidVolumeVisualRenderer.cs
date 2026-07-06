using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(48)]
public class SphFluidVolumeVisualRenderer : MonoBehaviour
{
    private const string RuntimeObjectName = "SphFluidVolumeVisualRenderer_Phase04";
    private const string SurfaceName = "SPH_Phase04_ContainedPaintSurface";
    private const string WallName = "SPH_Phase04_ContainedPaintBody";
    private const string RimName = "SPH_Phase04_MeniscusHighlight";

    public enum VisualQuality
    {
        Preview,
        Balanced,
        High,
        Ultra
    }

    [Header("Runtime Control")]
    public bool enableVisualVolume = true;
    public bool autoCreateChildren = true;
    public bool onlyShowWhileGpuSphRuns = true;
    public VisualQuality quality = VisualQuality.Ultra;
    [Range(0.02f, 0.20f)] public float rebuildInterval = 0.045f;

    [Header("Scene References")]
    public RealGpuSphController gpuSphController;
    public Transform bucketTransform;
    public Renderer bucketRenderer;
    public PaintEmitter paintEmitter;

    [Header("Volume Fit")]
    [Range(0.05f, 0.98f)] public float fallbackFill01 = 0.68f;
    [Range(0.45f, 1.05f)] public float radiusScale = 0.78f;
    [Range(0.0f, 0.08f)] public float rimInset = 0.018f;
    [Range(0.0f, 0.10f)] public float bottomInset = 0.025f;
    [Range(0.0f, 0.12f)] public float meniscusWidth = 0.038f;
    [Range(0.0f, 0.10f)] public float meniscusLift = 0.026f;

    [Header("Slosh Reconstruction")]
    public bool alignSurfaceToEffectiveGravity = true;
    [Range(0.0f, 1.4f)] public float tiltStrength = 0.58f;
    [Range(0.0f, 0.22f)] public float centerShiftStrength = 0.045f;
    [Range(0.01f, 0.50f)] public float sloshResponse = 0.18f;
    [Range(0.0f, 12.0f)] public float accelerationInfluence = 0.22f;
    [Range(0.0f, 0.08f)] public float waveAmplitude = 0.012f;
    [Range(0.0f, 18.0f)] public float waveFrequency = 7.5f;
    [Range(0.0f, 2.5f)] public float rimWaveGain = 1.35f;

    [Header("Mesh Budgets")]
    [Range(32, 192)] public int surfaceSegments = 104;
    [Range(4, 48)] public int surfaceRings = 20;
    [Range(2, 32)] public int wallSubdivisions = 10;

    [Header("Material")]
    public Color paintColor = new Color(0.62f, 0.012f, 0.004f, 0.98f);
    [Range(0.2f, 1.0f)] public float surfaceAlpha = 0.98f;
    [Range(0.05f, 1.0f)] public float bodyAlpha = 0.32f;
    [Range(0.0f, 1.0f)] public float rimAlpha = 1.0f;
    [Range(0.0f, 2.0f)] public float highlightStrength = 1.25f;
    [Range(0.0f, 1.0f)] public float depthDarkening = 0.22f;

    [Header("Runtime Readout")]
    [SerializeField] private int activeSurfaceVertices;
    [SerializeField] private float resolvedFill01;
    [SerializeField] private float sloshIntensity;
    [SerializeField] private string statusLine = "Phase 04 visual volume waiting.";

    private GameObject surfaceObject;
    private GameObject wallObject;
    private GameObject rimObject;
    private Mesh surfaceMesh;
    private Mesh wallMesh;
    private Mesh rimMesh;
    private Material surfaceMaterial;
    private Material wallMaterial;
    private Material rimMaterial;

    private Vector3 previousBucketPosition;
    private Vector3 previousBucketVelocity;
    private bool hasMotionState;
    private Vector3 filteredAccelerationWorld;
    private Vector3 filteredNormalLocal = Vector3.up;
    private Vector3 filteredCenterOffsetLocal;
    private float rebuildTimer;
    private int cachedSegments;
    private int cachedRings;
    private int cachedWallSubdivisions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphFluidVolumeVisualRenderer>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphFluidVolumeVisualRenderer>();
    }

    private void Awake()
    {
        AutoFindReferences();
        ApplyQualityDefaults(false);
        EnsureObjects();
    }

    private void Start()
    {
        AutoFindReferences();
        ApplyQualityDefaults(false);
        EnsureObjects();
        ForceRebuild();
    }

    private void LateUpdate()
    {
        AutoFindReferences();
        EnsureObjects();
        UpdateMotion(Time.deltaTime);
        SyncFromControllerAndEmitter();
        UpdateMaterials();

        bool shouldShow = enableVisualVolume && bucketTransform != null;
        if (shouldShow && onlyShowWhileGpuSphRuns && gpuSphController != null)
        {
            shouldShow = gpuSphController.enableRuntime;
        }

        SetChildrenActive(shouldShow);
        if (!shouldShow)
        {
            statusLine = "Phase 04 visual volume hidden until GPU SPH runs.";
            return;
        }

        rebuildTimer += Time.deltaTime;
        bool topologyChanged = cachedSegments != surfaceSegments || cachedRings != surfaceRings || cachedWallSubdivisions != wallSubdivisions;
        if (topologyChanged || rebuildTimer >= Mathf.Max(0.02f, rebuildInterval))
        {
            rebuildTimer = 0f;
            RebuildMeshes();
        }
    }

    public void ForceRebuild()
    {
        rebuildTimer = Mathf.Max(rebuildInterval, 0.02f);
        RebuildMeshes();
    }

    private void AutoFindReferences()
    {
        if (gpuSphController == null) gpuSphController = Object.FindFirstObjectByType<RealGpuSphController>();
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();

        if (bucketTransform == null && gpuSphController != null) bucketTransform = gpuSphController.bucketTransform;
        if (bucketRenderer == null && gpuSphController != null) bucketRenderer = gpuSphController.bucketRenderer;

        if (bucketTransform == null)
        {
            GameObject bucket = GameObject.Find("Bucket");
            if (bucket != null) bucketTransform = bucket.transform;
        }

        if (bucketRenderer == null && bucketTransform != null) bucketRenderer = bucketTransform.GetComponentInChildren<Renderer>();
    }

    private void ApplyQualityDefaults(bool force)
    {
        if (!force && cachedSegments > 0)
        {
            return;
        }

        switch (quality)
        {
            case VisualQuality.Preview:
                surfaceSegments = Mathf.Min(surfaceSegments, 56);
                surfaceRings = Mathf.Min(surfaceRings, 10);
                wallSubdivisions = Mathf.Min(wallSubdivisions, 5);
                rebuildInterval = Mathf.Max(rebuildInterval, 0.075f);
                break;
            case VisualQuality.Balanced:
                surfaceSegments = Mathf.Clamp(surfaceSegments, 64, 96);
                surfaceRings = Mathf.Clamp(surfaceRings, 12, 18);
                wallSubdivisions = Mathf.Clamp(wallSubdivisions, 6, 9);
                rebuildInterval = Mathf.Max(rebuildInterval, 0.055f);
                break;
            case VisualQuality.High:
                surfaceSegments = Mathf.Clamp(surfaceSegments, 88, 128);
                surfaceRings = Mathf.Clamp(surfaceRings, 16, 26);
                wallSubdivisions = Mathf.Clamp(wallSubdivisions, 8, 14);
                rebuildInterval = Mathf.Clamp(rebuildInterval, 0.035f, 0.075f);
                break;
            case VisualQuality.Ultra:
                surfaceSegments = Mathf.Clamp(surfaceSegments, 120, 176);
                surfaceRings = Mathf.Clamp(surfaceRings, 24, 40);
                wallSubdivisions = Mathf.Clamp(wallSubdivisions, 12, 22);
                rebuildInterval = Mathf.Clamp(rebuildInterval, 0.025f, 0.060f);
                break;
        }
    }

    private void EnsureObjects()
    {
        if (!autoCreateChildren || bucketTransform == null)
        {
            return;
        }

        Shader shader = Shader.Find("VR/Paint/SPH Volume Surface URP");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (surfaceObject == null)
        {
            surfaceObject = CreateChild(SurfaceName, ref surfaceMesh, ref surfaceMaterial, shader, surfaceAlpha, 3000);
        }

        if (wallObject == null)
        {
            wallObject = CreateChild(WallName, ref wallMesh, ref wallMaterial, shader, bodyAlpha, 2998);
        }

        if (rimObject == null)
        {
            rimObject = CreateChild(RimName, ref rimMesh, ref rimMaterial, shader, rimAlpha, 3001);
        }
    }

    private GameObject CreateChild(string childName, ref Mesh mesh, ref Material material, Shader shader, float alpha, int renderQueue)
    {
        Transform existing = bucketTransform != null ? bucketTransform.Find(childName) : null;
        GameObject obj = existing != null ? existing.gameObject : new GameObject(childName);
        obj.transform.SetParent(bucketTransform, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        MeshFilter filter = obj.GetComponent<MeshFilter>();
        if (filter == null) filter = obj.AddComponent<MeshFilter>();

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = obj.AddComponent<MeshRenderer>();

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = childName + "_Mesh";
            mesh.MarkDynamic();
        }
        filter.sharedMesh = mesh;

        if (material == null)
        {
            material = new Material(shader);
            material.name = childName + "_Material_Runtime";
            material.renderQueue = renderQueue;
        }
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        SetMaterialAlpha(material, alpha);
        return obj;
    }

    private void SetChildrenActive(bool active)
    {
        if (surfaceObject != null && surfaceObject.activeSelf != active) surfaceObject.SetActive(active);
        if (wallObject != null && wallObject.activeSelf != active) wallObject.SetActive(active);
        if (rimObject != null && rimObject.activeSelf != active) rimObject.SetActive(active);
    }

    private void SyncFromControllerAndEmitter()
    {
        if (gpuSphController != null)
        {
            paintColor = gpuSphController.paintColor;
            fallbackFill01 = Mathf.Clamp(gpuSphController.initialFill01, 0.05f, 0.98f);
        }

        if (paintEmitter != null)
        {
            fallbackFill01 = Mathf.Clamp(paintEmitter.PaintFill01, 0.05f, 0.98f);
        }

        resolvedFill01 = fallbackFill01;
    }

    private void UpdateMotion(float deltaTime)
    {
        if (bucketTransform == null)
        {
            filteredAccelerationWorld = Vector3.zero;
            filteredNormalLocal = Vector3.up;
            filteredCenterOffsetLocal = Vector3.zero;
            return;
        }

        float dt = Mathf.Max(deltaTime, 0.0001f);
        Vector3 current = bucketTransform.position;
        Vector3 velocity = Vector3.zero;

        if (hasMotionState)
        {
            velocity = (current - previousBucketPosition) / dt;
            Vector3 acceleration = (velocity - previousBucketVelocity) / dt;
            filteredAccelerationWorld = Vector3.Lerp(filteredAccelerationWorld, acceleration, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, sloshResponse)));
        }
        else
        {
            hasMotionState = true;
        }

        previousBucketPosition = current;
        previousBucketVelocity = velocity;

        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.001f ? Physics.gravity : new Vector3(0f, -9.81f, 0f);
        Vector3 effectiveUpWorld = -gravity - filteredAccelerationWorld * accelerationInfluence;
        if (effectiveUpWorld.sqrMagnitude < 0.001f) effectiveUpWorld = Vector3.up;
        effectiveUpWorld.Normalize();

        Vector3 targetNormalLocal = alignSurfaceToEffectiveGravity ? bucketTransform.InverseTransformDirection(effectiveUpWorld).normalized : Vector3.up;
        targetNormalLocal = Vector3.Slerp(Vector3.up, targetNormalLocal, tiltStrength).normalized;
        filteredNormalLocal = Vector3.Slerp(filteredNormalLocal, targetNormalLocal, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, sloshResponse))).normalized;
        if (filteredNormalLocal.y < 0.22f) filteredNormalLocal = Vector3.Slerp(filteredNormalLocal, Vector3.up, 0.35f).normalized;

        Vector3 localAcceleration = bucketTransform.InverseTransformDirection(filteredAccelerationWorld);
        Vector3 lateral = new Vector3(-localAcceleration.x, 0f, -localAcceleration.z);
        float radius = ResolveRadius();
        Vector3 targetOffset = Vector3.ClampMagnitude(lateral * centerShiftStrength * 0.025f, radius * 0.28f);
        filteredCenterOffsetLocal = Vector3.Lerp(filteredCenterOffsetLocal, targetOffset, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, sloshResponse * 1.3f)));
        sloshIntensity = Mathf.Clamp01(filteredAccelerationWorld.magnitude / 12f + Vector3.Angle(filteredNormalLocal, Vector3.up) / 55f);
    }

    private void UpdateMaterials()
    {
        SetMaterial(surfaceMaterial, surfaceAlpha, 1.0f);
        SetMaterial(wallMaterial, bodyAlpha, 0.82f);
        SetMaterial(rimMaterial, rimAlpha, 1.22f);
    }

    private void SetMaterial(Material material, float alpha, float brightness)
    {
        if (material == null) return;
        Color c = paintColor;
        c.r = Mathf.Clamp01(c.r * brightness);
        c.g = Mathf.Clamp01(c.g * brightness);
        c.b = Mathf.Clamp01(c.b * brightness);
        c.a = alpha;
        material.SetColor("_BaseColor", c);
        material.SetFloat("_Alpha", alpha);
        material.SetFloat("_HighlightStrength", highlightStrength);
        material.SetFloat("_DepthDarkening", depthDarkening);
        material.SetFloat("_SloshIntensity", sloshIntensity);
    }

    private void SetMaterialAlpha(Material material, float alpha)
    {
        if (material != null)
        {
            Color c = paintColor;
            c.a = alpha;
            material.SetColor("_BaseColor", c);
            material.SetFloat("_Alpha", alpha);
        }
    }

    private void RebuildMeshes()
    {
        if (surfaceMesh == null || wallMesh == null || rimMesh == null || bucketTransform == null)
        {
            return;
        }

        ApplyQualityDefaults(false);
        int seg = Mathf.Clamp(surfaceSegments, 32, 192);
        int rings = Mathf.Clamp(surfaceRings, 4, 48);
        int wallSteps = Mathf.Clamp(wallSubdivisions, 2, 32);
        float radius = ResolveRadius();
        float bottomY = ResolveBottomY() + bottomInset;
        float rimY = ResolveRimY() - rimInset;
        float fillY = Mathf.Lerp(bottomY + 0.03f, rimY, Mathf.Clamp01(resolvedFill01));
        Vector3 center = new Vector3(filteredCenterOffsetLocal.x, fillY, filteredCenterOffsetLocal.z);

        BuildSurfaceMesh(surfaceMesh, seg, rings, radius, center, bottomY, rimY);
        BuildWallMesh(wallMesh, seg, wallSteps, radius, center, bottomY, rimY);
        BuildRimMesh(rimMesh, seg, radius, center, bottomY, rimY);

        cachedSegments = seg;
        cachedRings = rings;
        cachedWallSubdivisions = wallSteps;
        activeSurfaceVertices = surfaceMesh.vertexCount;
        statusLine = "Phase04 visual volume | " + quality + " | surface verts " + activeSurfaceVertices + " | fill " + (resolvedFill01 * 100f).ToString("0") + "% | slosh " + (sloshIntensity * 100f).ToString("0") + "%";
    }

    private float ResolveRadius()
    {
        if (gpuSphController != null)
        {
            return Mathf.Max(0.025f, gpuSphController.bucketRadiusLocal * radiusScale - rimInset);
        }

        if (bucketRenderer != null && bucketTransform != null)
        {
            float scale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(bucketTransform.lossyScale.x), Mathf.Abs(bucketTransform.lossyScale.z)));
            return Mathf.Max(0.025f, Mathf.Max(bucketRenderer.bounds.extents.x, bucketRenderer.bounds.extents.z) / scale * 0.55f);
        }

        return 0.30f;
    }

    private float ResolveBottomY()
    {
        if (gpuSphController != null) return gpuSphController.bucketBottomLocalY;
        return -0.42f;
    }

    private float ResolveRimY()
    {
        if (gpuSphController != null) return gpuSphController.bucketRimLocalY;
        return 0.42f;
    }

    private float SurfaceYAt(float x, float z, Vector3 center, float bottomY, float rimY, float radial01, float angle)
    {
        Vector3 n = filteredNormalLocal.sqrMagnitude > 0.001f ? filteredNormalLocal.normalized : Vector3.up;
        float y = center.y;
        if (Mathf.Abs(n.y) > 0.08f)
        {
            y = center.y - (n.x * (x - center.x) + n.z * (z - center.z)) / n.y;
        }

        float wave = Mathf.Sin(Time.time * waveFrequency + angle * 2.0f + radial01 * 5.2f) * waveAmplitude * sloshIntensity;
        wave += Mathf.Sin(Time.time * (waveFrequency * 0.67f) - angle * 3.0f) * waveAmplitude * 0.45f * sloshIntensity * Mathf.Lerp(0.2f, rimWaveGain, radial01);
        y += wave;
        return Mathf.Clamp(y, bottomY + 0.015f, rimY + meniscusLift);
    }

    private void BuildSurfaceMesh(Mesh mesh, int seg, int rings, float radius, Vector3 center, float bottomY, float rimY)
    {
        int vertexCount = 1 + seg * rings;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[seg * 3 + (rings - 1) * seg * 6];

        vertices[0] = center;
        normals[0] = filteredNormalLocal.normalized;
        uv[0] = new Vector2(0.5f, 0.5f);

        int v = 1;
        for (int r = 1; r <= rings; r++)
        {
            float radial01 = r / (float)rings;
            float rr = radius * radial01;
            for (int s = 0; s < seg; s++)
            {
                float a = s * Mathf.PI * 2f / seg;
                float x = center.x + Mathf.Cos(a) * rr;
                float z = center.z + Mathf.Sin(a) * rr;
                float y = SurfaceYAt(x, z, center, bottomY, rimY, radial01, a);
                vertices[v] = new Vector3(x, y, z);
                normals[v] = filteredNormalLocal.normalized;
                uv[v] = new Vector2(0.5f + Mathf.Cos(a) * radial01 * 0.5f, 0.5f + Mathf.Sin(a) * radial01 * 0.5f);
                v++;
            }
        }

        int t = 0;
        for (int s = 0; s < seg; s++)
        {
            triangles[t++] = 0;
            triangles[t++] = 1 + ((s + 1) % seg);
            triangles[t++] = 1 + s;
        }

        for (int r = 1; r < rings; r++)
        {
            int inner = 1 + (r - 1) * seg;
            int outer = 1 + r * seg;
            for (int s = 0; s < seg; s++)
            {
                int s1 = (s + 1) % seg;
                triangles[t++] = inner + s;
                triangles[t++] = inner + s1;
                triangles[t++] = outer + s1;
                triangles[t++] = inner + s;
                triangles[t++] = outer + s1;
                triangles[t++] = outer + s;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void BuildWallMesh(Mesh mesh, int seg, int wallSteps, float radius, Vector3 center, float bottomY, float rimY)
    {
        int rows = wallSteps + 1;
        Vector3[] vertices = new Vector3[rows * seg];
        Vector3[] normals = new Vector3[rows * seg];
        Vector2[] uv = new Vector2[rows * seg];
        int[] triangles = new int[wallSteps * seg * 6];

        for (int yStep = 0; yStep < rows; yStep++)
        {
            float v01 = yStep / (float)wallSteps;
            for (int s = 0; s < seg; s++)
            {
                float a = s * Mathf.PI * 2f / seg;
                float x = center.x + Mathf.Cos(a) * radius;
                float z = center.z + Mathf.Sin(a) * radius;
                float topY = SurfaceYAt(x, z, center, bottomY, rimY, 1f, a);
                float y = Mathf.Lerp(bottomY, topY, v01);
                int index = yStep * seg + s;
                vertices[index] = new Vector3(x, y, z);
                normals[index] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                uv[index] = new Vector2(s / (float)seg, v01);
            }
        }

        int t = 0;
        for (int yStep = 0; yStep < wallSteps; yStep++)
        {
            int row = yStep * seg;
            int nextRow = (yStep + 1) * seg;
            for (int s = 0; s < seg; s++)
            {
                int s1 = (s + 1) % seg;
                triangles[t++] = row + s;
                triangles[t++] = nextRow + s;
                triangles[t++] = nextRow + s1;
                triangles[t++] = row + s;
                triangles[t++] = nextRow + s1;
                triangles[t++] = row + s1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void BuildRimMesh(Mesh mesh, int seg, float radius, Vector3 center, float bottomY, float rimY)
    {
        float inner = Mathf.Max(0.001f, radius - meniscusWidth);
        float outer = radius + meniscusWidth * 0.15f;
        Vector3[] vertices = new Vector3[seg * 2];
        Vector3[] normals = new Vector3[seg * 2];
        Vector2[] uv = new Vector2[seg * 2];
        int[] triangles = new int[seg * 6];

        for (int s = 0; s < seg; s++)
        {
            float a = s * Mathf.PI * 2f / seg;
            float ca = Mathf.Cos(a);
            float sa = Mathf.Sin(a);
            float yInner = SurfaceYAt(center.x + ca * inner, center.z + sa * inner, center, bottomY, rimY, 0.94f, a) + meniscusLift;
            float yOuter = SurfaceYAt(center.x + ca * outer, center.z + sa * outer, center, bottomY, rimY, 1.0f, a) + meniscusLift * 0.6f;
            int i0 = s * 2;
            vertices[i0] = new Vector3(center.x + ca * inner, yInner, center.z + sa * inner);
            vertices[i0 + 1] = new Vector3(center.x + ca * outer, yOuter, center.z + sa * outer);
            normals[i0] = filteredNormalLocal.normalized;
            normals[i0 + 1] = filteredNormalLocal.normalized;
            uv[i0] = new Vector2(s / (float)seg, 0f);
            uv[i0 + 1] = new Vector2(s / (float)seg, 1f);
        }

        int t = 0;
        for (int s = 0; s < seg; s++)
        {
            int s1 = (s + 1) % seg;
            int a0 = s * 2;
            int a1 = s1 * 2;
            triangles[t++] = a0;
            triangles[t++] = a1;
            triangles[t++] = a1 + 1;
            triangles[t++] = a0;
            triangles[t++] = a1 + 1;
            triangles[t++] = a0 + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    public string StatusLine => statusLine;
    public string VisualQualityLine => "Phase04 volume " + quality + " | verts " + activeSurfaceVertices + " | fill " + (resolvedFill01 * 100f).ToString("0") + "% | slosh " + (sloshIntensity * 100f).ToString("0") + "%";
    public int ActiveSurfaceVertices => activeSurfaceVertices;
    public float ResolvedFill01 => resolvedFill01;
    public float SloshIntensity => sloshIntensity;
}
