using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(70)]
public class SphHeroPaintVisualRenderer : MonoBehaviour
{
    private const string RuntimeObjectName = "SphHeroPaintVisualRenderer_Phase04B";
    private const string StreamObjectName = "SPH_Hero_CohesivePaintStream";
    private const string ImpactObjectName = "SPH_Hero_GlossyImpactPool";
    private const string DropletRootName = "SPH_Hero_Droplets";

    [Header("Runtime Control")]
    public bool enableHeroVisuals = true;
    public bool onlyShowWhileGpuSphRuns = true;
    public bool showStreamWhenPouring = true;
    public bool showImpactPool = true;
    public bool showHeroDroplets = true;

    [Header("Scene References")]
    public RealGpuSphController gpuSphController;
    public Transform bucketTransform;
    public Renderer bucketRenderer;
    public CanvasPainter canvasPainter;
    public PaintEmitter paintEmitter;

    [Header("Art Direction")]
    public Color paintColor = new Color(0.55f, 0.006f, 0.002f, 0.98f);
    [Range(0.10f, 0.90f)] public float visualPourTiltThreshold = 0.38f;
    [Range(0.10f, 2.00f)] public float streamLengthGain = 0.92f;
    [Range(0.005f, 0.080f)] public float streamStartRadius = 0.026f;
    [Range(0.005f, 0.090f)] public float streamEndRadius = 0.038f;
    [Range(0.0f, 0.12f)] public float streamBreakup = 0.018f;
    [Range(0.0f, 0.18f)] public float streamGloss = 0.095f;
    [Range(0.0f, 1.0f)] public float velocityResponse = 0.18f;
    [Range(0.1f, 1.5f)] public float impactPoolRadius = 0.34f;
    [Range(0.0f, 0.04f)] public float surfaceYOffset = 0.018f;

    [Header("Mesh Budgets")]
    [Range(8, 56)] public int streamSegments = 18;
    [Range(5, 20)] public int streamSides = 9;
    [Range(0, 96)] public int dropletCount = 22;
    [Range(20, 128)] public int impactSegments = 64;

    [Header("Runtime Readout")]
    [SerializeField] private bool visualPouring;
    [SerializeField] private float tilt01;
    [SerializeField] private string statusLine = "Hero visual renderer waiting.";

    private GameObject streamObject;
    private GameObject impactObject;
    private GameObject dropletRoot;
    private Mesh streamMesh;
    private Mesh impactMesh;
    private Material streamMaterial;
    private Material impactMaterial;
    private Material dropletMaterial;
    private readonly GameObject[] droplets = new GameObject[96];
    private Vector3 previousBucketPosition;
    private Vector3 bucketVelocity;
    private bool hasBucketState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphHeroPaintVisualRenderer>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphHeroPaintVisualRenderer>();
    }

    private void Awake()
    {
        AutoFindReferences();
        EnsureObjects();
    }

    private void Start()
    {
        AutoFindReferences();
        EnsureObjects();
        RebuildAll(true);
    }

    private void LateUpdate()
    {
        AutoFindReferences();
        EnsureObjects();
        UpdateBucketMotion(Time.deltaTime);

        bool gpuRunning = gpuSphController == null || gpuSphController.enableRuntime;
        bool shouldRun = enableHeroVisuals && bucketTransform != null && (!onlyShowWhileGpuSphRuns || gpuRunning);

        if (!shouldRun)
        {
            SetVisualsActive(false);
            statusLine = "Hero paint visuals hidden until GPU SPH preview runs.";
            return;
        }

        PourFrame frame = BuildPourFrame();
        visualPouring = showStreamWhenPouring && frame.pouring;

        if (!visualPouring)
        {
            SetObjectActive(streamObject, false);
            SetObjectActive(impactObject, false);
            SetDropletsActive(false);
            statusLine = "Hero paint visuals ready | contained fluid only.";
            return;
        }

        SetObjectActive(streamObject, true);
        SetObjectActive(impactObject, showImpactPool);
        SetDropletsActive(showHeroDroplets);

        UpdateMaterials(frame.intensity01);
        BuildStreamMesh(frame);
        if (showImpactPool)
        {
            BuildImpactMesh(frame);
        }
        if (showHeroDroplets)
        {
            UpdateDroplets(frame);
        }

        statusLine = "Hero cohesive stream | tilt " + (tilt01 * 100f).ToString("0") + "% | droplets " + Mathf.Clamp(dropletCount, 0, droplets.Length);
    }

    private void AutoFindReferences()
    {
        if (gpuSphController == null) gpuSphController = Object.FindFirstObjectByType<RealGpuSphController>();
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();

        if (bucketTransform == null && gpuSphController != null) bucketTransform = gpuSphController.bucketTransform;
        if (bucketRenderer == null && gpuSphController != null) bucketRenderer = gpuSphController.bucketRenderer;

        if (bucketTransform == null)
        {
            GameObject bucket = GameObject.Find("Bucket");
            if (bucket != null) bucketTransform = bucket.transform;
        }

        if (bucketRenderer == null && bucketTransform != null)
        {
            bucketRenderer = bucketTransform.GetComponentInChildren<Renderer>();
        }
    }

    private void EnsureObjects()
    {
        Shader shader = Shader.Find("VR/Paint/Hero Paint Surface URP");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (streamMaterial == null)
        {
            streamMaterial = new Material(shader);
            streamMaterial.name = "Runtime_Hero_CohesivePaintStream";
            streamMaterial.renderQueue = 3020;
        }
        if (impactMaterial == null)
        {
            impactMaterial = new Material(shader);
            impactMaterial.name = "Runtime_Hero_GlossyPaintPool";
            impactMaterial.renderQueue = 3010;
        }
        if (dropletMaterial == null)
        {
            dropletMaterial = new Material(shader);
            dropletMaterial.name = "Runtime_Hero_PaintDroplets";
            dropletMaterial.renderQueue = 3030;
        }

        if (streamObject == null)
        {
            streamObject = CreateMeshObject(StreamObjectName, ref streamMesh, streamMaterial);
        }
        if (impactObject == null)
        {
            impactObject = CreateMeshObject(ImpactObjectName, ref impactMesh, impactMaterial);
        }
        if (dropletRoot == null)
        {
            dropletRoot = GameObject.Find(DropletRootName);
            if (dropletRoot == null) dropletRoot = new GameObject(DropletRootName);
        }

        EnsureDropletPool();
    }

    private GameObject CreateMeshObject(string name, ref Mesh mesh, Material material)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null) obj = new GameObject(name);
        obj.transform.position = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        MeshFilter filter = obj.GetComponent<MeshFilter>();
        if (filter == null) filter = obj.AddComponent<MeshFilter>();
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = obj.AddComponent<MeshRenderer>();

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = name + "_Mesh";
            mesh.MarkDynamic();
        }

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return obj;
    }

    private void EnsureDropletPool()
    {
        int count = Mathf.Clamp(dropletCount, 0, droplets.Length);
        for (int i = 0; i < count; i++)
        {
            if (droplets[i] != null) continue;
            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            d.name = "SPH_Hero_Droplet_" + i.ToString("00");
            d.transform.SetParent(dropletRoot.transform, true);
            Collider col = d.GetComponent<Collider>();
            if (col != null) Destroy(col);
            MeshRenderer renderer = d.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = dropletMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            droplets[i] = d;
        }
    }

    private void RebuildAll(bool force)
    {
        if (!force || bucketTransform == null) return;
        PourFrame frame = BuildPourFrame();
        UpdateMaterials(frame.intensity01);
        BuildStreamMesh(frame);
        BuildImpactMesh(frame);
        UpdateDroplets(frame);
    }

    private void UpdateBucketMotion(float deltaTime)
    {
        if (bucketTransform == null)
        {
            bucketVelocity = Vector3.zero;
            hasBucketState = false;
            return;
        }

        float dt = Mathf.Max(deltaTime, 0.0001f);
        if (hasBucketState)
        {
            bucketVelocity = Vector3.Lerp(bucketVelocity, (bucketTransform.position - previousBucketPosition) / dt, 1f - Mathf.Exp(-dt / 0.08f));
        }
        else
        {
            bucketVelocity = Vector3.zero;
            hasBucketState = true;
        }
        previousBucketPosition = bucketTransform.position;
    }

    private PourFrame BuildPourFrame()
    {
        PourFrame frame = new PourFrame();
        if (bucketTransform == null)
        {
            return frame;
        }

        float radius = ResolveBucketRadius();
        float rimY = ResolveBucketRimY();
        Vector3 gravity = Physics.gravity.sqrMagnitude > 0.001f ? Physics.gravity.normalized : Vector3.down;
        Vector3 gravityLocal3 = bucketTransform.InverseTransformDirection(gravity);
        Vector2 gravityLocal = new Vector2(gravityLocal3.x, gravityLocal3.z);
        tilt01 = Mathf.Clamp01(gravityLocal.magnitude);

        Vector2 downhill = gravityLocal.sqrMagnitude > 0.0001f ? gravityLocal.normalized : new Vector2(0f, 1f);
        Vector3 rimLocal = new Vector3(downhill.x * radius * 0.92f, rimY + 0.012f, downhill.y * radius * 0.92f);
        frame.source = bucketTransform.TransformPoint(rimLocal);

        Vector3 outLocal = new Vector3(downhill.x, -0.18f, downhill.y).normalized;
        Vector3 outWorld = bucketTransform.TransformDirection(outLocal).normalized;
        Vector3 streamDir = Vector3.Slerp(outWorld, gravity, 0.42f).normalized;
        if (streamDir.y > -0.08f) streamDir = Vector3.Slerp(streamDir, gravity, 0.70f).normalized;

        float planeY = ResolveSurfaceY();
        float t = Mathf.Clamp((frame.source.y - planeY) / Mathf.Max(0.08f, -streamDir.y), 0.22f, 3.5f);
        Vector3 rawTarget = frame.source + streamDir * t * streamLengthGain;
        rawTarget.y = planeY;

        Vector3 canvasCenter = ResolveCanvasCenter(planeY);
        float canvasBlend = canvasPainter != null ? 0.28f : 0.0f;
        frame.target = Vector3.Lerp(rawTarget, canvasCenter, canvasBlend);
        frame.target.y = planeY;

        Vector3 chord = frame.target - frame.source;
        Vector3 lateral = Vector3.Cross(chord.normalized, Vector3.up);
        if (lateral.sqrMagnitude < 0.001f) lateral = Vector3.right;
        lateral.Normalize();

        float motion = Mathf.Clamp01(bucketVelocity.magnitude * velocityResponse);
        float curvePush = Mathf.Lerp(0.03f, 0.18f, Mathf.Clamp01(tilt01 + motion));
        frame.controlA = frame.source + streamDir * Mathf.Max(0.08f, chord.magnitude * 0.28f) + lateral * Mathf.Sin(Time.time * 2.1f) * streamBreakup;
        frame.controlB = Vector3.Lerp(frame.source, frame.target, 0.72f) + gravity * curvePush + lateral * Mathf.Sin(Time.time * 1.7f + 1.3f) * streamBreakup * 0.75f;
        frame.normal = Vector3.up;
        frame.intensity01 = Mathf.Clamp01((tilt01 - visualPourTiltThreshold) / Mathf.Max(0.001f, 1f - visualPourTiltThreshold));
        frame.intensity01 = Mathf.SmoothStep(0f, 1f, frame.intensity01);
        frame.pouring = frame.intensity01 > 0.02f && ResolveFill01() > 0.035f;
        return frame;
    }

    private float ResolveBucketRadius()
    {
        if (gpuSphController != null) return Mathf.Max(0.04f, gpuSphController.bucketRadiusLocal);
        if (bucketRenderer != null && bucketTransform != null)
        {
            float scale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(bucketTransform.lossyScale.x), Mathf.Abs(bucketTransform.lossyScale.z)));
            return Mathf.Max(0.04f, Mathf.Max(bucketRenderer.bounds.extents.x, bucketRenderer.bounds.extents.z) / scale * 0.58f);
        }
        return 0.28f;
    }

    private float ResolveBucketRimY()
    {
        if (gpuSphController != null) return gpuSphController.bucketRimLocalY;
        return 0.42f;
    }

    private float ResolveFill01()
    {
        if (paintEmitter != null) return Mathf.Clamp01(paintEmitter.PaintFill01);
        if (gpuSphController != null) return Mathf.Clamp01(gpuSphController.initialFill01);
        return 0.70f;
    }

    private float ResolveSurfaceY()
    {
        if (canvasPainter != null) return canvasPainter.transform.position.y + surfaceYOffset;
        if (gpuSphController != null && gpuSphController.surfacePlane != null) return gpuSphController.surfacePlane.position.y + surfaceYOffset;
        return surfaceYOffset;
    }

    private Vector3 ResolveCanvasCenter(float planeY)
    {
        if (canvasPainter != null)
        {
            Vector3 center = canvasPainter.transform.position;
            center.y = planeY;
            return center;
        }
        if (gpuSphController != null && gpuSphController.surfacePlane != null)
        {
            Vector3 center = gpuSphController.surfacePlane.position;
            center.y = planeY;
            return center;
        }
        Vector3 fallback = bucketTransform != null ? bucketTransform.position + Vector3.down * 0.7f : Vector3.zero;
        fallback.y = planeY;
        return fallback;
    }

    private void BuildStreamMesh(PourFrame frame)
    {
        if (streamMesh == null) return;
        int seg = Mathf.Clamp(streamSegments, 8, 40);
        int sides = Mathf.Clamp(streamSides, 5, 16);
        int vertCount = (seg + 1) * sides;
        int triCount = seg * sides * 6;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        int[] triangles = new int[triCount];

        Vector3 previousNormal = Vector3.up;
        for (int i = 0; i <= seg; i++)
        {
            float u = i / (float)seg;
            Vector3 p = Bezier(frame.source, frame.controlA, frame.controlB, frame.target, u);
            Vector3 tangent = BezierTangent(frame.source, frame.controlA, frame.controlB, frame.target, u);
            Vector3 right = Vector3.Cross(previousNormal, tangent);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(Vector3.right, tangent);
            right.Normalize();
            Vector3 normal = Vector3.Cross(tangent, right).normalized;
            previousNormal = normal;

            float radius = Mathf.Lerp(streamStartRadius, streamEndRadius, Mathf.SmoothStep(0f, 1f, u));
            radius *= Mathf.Lerp(0.60f, 1.0f, frame.intensity01);
            radius *= 1.0f + Mathf.Sin(u * Mathf.PI * 4.0f + Time.time * 8.0f) * streamGloss;

            for (int s = 0; s < sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                Vector3 radial = right * Mathf.Cos(a) + normal * Mathf.Sin(a);
                int idx = i * sides + s;
                vertices[idx] = p + radial * radius;
                normals[idx] = radial.normalized;
                uv[idx] = new Vector2(u, s / (float)sides);
            }
        }

        int t = 0;
        for (int i = 0; i < seg; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a = i * sides + s;
                int b = i * sides + (s + 1) % sides;
                int c = (i + 1) * sides + s;
                int d = (i + 1) * sides + (s + 1) % sides;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }

        streamMesh.Clear();
        streamMesh.vertices = vertices;
        streamMesh.normals = normals;
        streamMesh.uv = uv;
        streamMesh.triangles = triangles;
        streamMesh.RecalculateBounds();
    }

    private void BuildImpactMesh(PourFrame frame)
    {
        if (impactMesh == null) return;
        int seg = Mathf.Clamp(impactSegments, 20, 96);
        Vector3[] vertices = new Vector3[seg + 1];
        Vector3[] normals = new Vector3[seg + 1];
        Vector2[] uv = new Vector2[seg + 1];
        int[] triangles = new int[seg * 3];

        float radiusA = impactPoolRadius * Mathf.Lerp(0.45f, 1.0f, frame.intensity01);
        float radiusB = radiusA * 0.48f;
        Vector3 dir = frame.target - frame.source;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

        vertices[0] = frame.target + Vector3.up * 0.002f;
        normals[0] = Vector3.up;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            float wobble = 1f + Mathf.Sin(a * 5f + Time.time * 1.8f) * 0.035f + Mathf.Sin(a * 9f) * 0.025f;
            Vector3 p = frame.target + dir * Mathf.Cos(a) * radiusA * wobble + side * Mathf.Sin(a) * radiusB * wobble;
            p.y += 0.002f;
            vertices[i + 1] = p;
            normals[i + 1] = Vector3.up;
            uv[i + 1] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i == seg - 1 ? 1 : i + 2;
        }

        impactMesh.Clear();
        impactMesh.vertices = vertices;
        impactMesh.normals = normals;
        impactMesh.uv = uv;
        impactMesh.triangles = triangles;
        impactMesh.RecalculateBounds();
    }

    private void UpdateDroplets(PourFrame frame)
    {
        EnsureDropletPool();
        int count = Mathf.Clamp(dropletCount, 0, droplets.Length);
        Vector3 pathSide = Vector3.Cross((frame.target - frame.source).normalized, Vector3.up);
        if (pathSide.sqrMagnitude < 0.001f) pathSide = Vector3.right;
        pathSide.Normalize();

        for (int i = 0; i < droplets.Length; i++)
        {
            if (droplets[i] == null) continue;
            bool active = i < count;
            droplets[i].SetActive(active);
            if (!active) continue;

            float seed = i * 12.9898f + 78.233f;
            float u = Mathf.Repeat(i * 0.173f + Time.time * (0.52f + (i % 5) * 0.065f), 1.0f);
            u = Mathf.Lerp(0.10f, 0.96f, u);
            Vector3 p = Bezier(frame.source, frame.controlA, frame.controlB, frame.target, u);
            float spread = Mathf.Lerp(0.010f, 0.115f, u) * frame.intensity01;
            Vector3 jitter = pathSide * Mathf.Sin(seed + Time.time * 3.1f) * spread + Vector3.up * Mathf.Cos(seed * 0.7f + Time.time * 2.4f) * spread * 0.35f;
            droplets[i].transform.position = p + jitter;
            float scale = Mathf.Lerp(0.012f, 0.046f, Hash01(i)) * Mathf.Lerp(0.62f, 1.10f, frame.intensity01);
            droplets[i].transform.localScale = Vector3.one * scale;
        }
    }

    private void UpdateMaterials(float intensity)
    {
        paintColor = gpuSphController != null ? gpuSphController.paintColor : paintColor;
        SetMaterial(streamMaterial, paintColor, Mathf.Lerp(0.76f, 0.98f, intensity), 1.18f, 1.0f);
        SetMaterial(impactMaterial, paintColor, Mathf.Lerp(0.58f, 0.88f, intensity), 0.95f, 0.75f);
        SetMaterial(dropletMaterial, paintColor, Mathf.Lerp(0.72f, 0.96f, intensity), 1.25f, 1.0f);
    }

    private void SetMaterial(Material material, Color color, float alpha, float brightness, float gloss)
    {
        if (material == null) return;
        Color c = color;
        c.r = Mathf.Clamp01(c.r * brightness);
        c.g = Mathf.Clamp01(c.g * brightness);
        c.b = Mathf.Clamp01(c.b * brightness);
        c.a = alpha;
        material.SetColor("_BaseColor", c);
        material.SetFloat("_Alpha", alpha);
        material.SetFloat("_GlossBoost", gloss);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        Vector3 tangent = 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.down;
    }

    private static float Hash01(int n)
    {
        float x = Mathf.Sin(n * 12.9898f + 78.233f) * 43758.5453f;
        return x - Mathf.Floor(x);
    }

    private void SetVisualsActive(bool active)
    {
        SetObjectActive(streamObject, active && visualPouring);
        SetObjectActive(impactObject, active && visualPouring && showImpactPool);
        SetDropletsActive(active && visualPouring && showHeroDroplets);
    }

    private void SetObjectActive(GameObject obj, bool active)
    {
        if (obj != null && obj.activeSelf != active) obj.SetActive(active);
    }

    private void SetDropletsActive(bool active)
    {
        if (dropletRoot != null && dropletRoot.activeSelf != active) dropletRoot.SetActive(active);
        for (int i = 0; i < droplets.Length; i++)
        {
            if (droplets[i] != null && droplets[i].activeSelf != active && (!active || i < dropletCount))
            {
                droplets[i].SetActive(active && i < dropletCount);
            }
        }
    }

    public string StatusLine => statusLine;
    public bool IsPouring => visualPouring;

    private struct PourFrame
    {
        public bool pouring;
        public float intensity01;
        public Vector3 source;
        public Vector3 controlA;
        public Vector3 controlB;
        public Vector3 target;
        public Vector3 normal;
    }
}
