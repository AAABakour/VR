using UnityEngine;

[DefaultExecutionOrder(115)]
public class BucketInternalFluidVisual : MonoBehaviour
{
    [Header("Sources")]
    public Transform bucketRoot;
    public BucketPaintReservoir reservoir;
    public BucketMotionDataProvider motionDataProvider;
    public BucketCollisionProxy bucketCollisionProxy;

    [Header("Reservoir Surface")]
    public float minLocalY = -0.42f;
    public float maxLocalY = 0.28f;
    [Min(0.01f)]
    public float radius = 0.36f;
    public Color color = new Color(1f, 0.24f, 0.04f, 0.72f);
    [Range(0f, 1f)]
    public float alpha = 0.72f;
    [Range(0f, 1f)]
    public float emptyAlpha = 0.02f;
    public Material material;
    [Min(8)]
    public int segments = 48;

    [Header("Slosh Visual Only")]
    [Range(0f, 1f)]
    public float sloshStrength = 0.18f;
    [Min(0.001f)]
    public float sloshDamping = 0.12f;
    [Range(0f, 20f)]
    public float maxSloshAngle = 9f;
    public bool showDebugGizmos = true;

    private GameObject visualObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private Vector2 smoothedSlosh;
    private Vector2 sloshVelocity;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        ResolveReferences();
        EnsureVisual();
        ApplyVisualState(true);
    }

    private void LateUpdate()
    {
        ApplyVisualState(false);
    }

    private void OnDisable()
    {
        if (visualObject != null)
        {
            visualObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (visualObject != null)
        {
            visualObject.SetActive(true);
        }
    }

    private void OnValidate()
    {
        segments = Mathf.Max(8, segments);
        radius = Mathf.Max(0.01f, radius);
        if (Application.isPlaying)
        {
            EnsureVisual();
            ApplyVisualState(true);
        }
    }

    [ContextMenu("Resolve Internal Fluid Visual References")]
    public void ResolveReferences()
    {
        if (bucketCollisionProxy == null)
        {
            bucketCollisionProxy = Object.FindFirstObjectByType<BucketCollisionProxy>();
        }

        if (bucketRoot == null)
        {
            bucketRoot = bucketCollisionProxy != null ? bucketCollisionProxy.BucketRoot : transform;
        }

        if (reservoir == null)
        {
            reservoir = Object.FindFirstObjectByType<BucketPaintReservoir>();
        }

        if (motionDataProvider == null)
        {
            motionDataProvider = Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }

        if (bucketCollisionProxy != null)
        {
            minLocalY = bucketCollisionProxy.bottomOffset + 0.08f;
            maxLocalY = bucketCollisionProxy.localCenter.y + bucketCollisionProxy.height * 0.35f;
            radius = Mathf.Max(0.05f, bucketCollisionProxy.radius - bucketCollisionProxy.wallThickness * 2.5f);
        }
    }

    public void ResetVisual()
    {
        smoothedSlosh = Vector2.zero;
        sloshVelocity = Vector2.zero;
        ApplyVisualState(true);
    }

    private void EnsureVisual()
    {
        if (visualObject == null)
        {
            Transform existing = transform.Find("BucketInternalPaintVisual");
            visualObject = existing != null ? existing.gameObject : new GameObject("BucketInternalPaintVisual");
            visualObject.transform.SetParent(bucketRoot != null ? bucketRoot : transform, false);
        }

        if (meshFilter == null)
        {
            meshFilter = visualObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = visualObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = visualObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = visualObject.AddComponent<MeshRenderer>();
            }
        }

        if (mesh == null || mesh.vertexCount != segments + 1)
        {
            mesh = BuildDiskMesh();
            meshFilter.sharedMesh = mesh;
        }

        meshRenderer.sharedMaterial = material;
        visualObject.SetActive(enabled);
    }

    private Mesh BuildDiskMesh()
    {
        Mesh disk = new Mesh();
        disk.name = "Bucket Reservoir Surface Visual";

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices[i + 1] = new Vector3(x, 0f, z);
            uv[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == segments - 1 ? 1 : i + 2;
        }

        disk.vertices = vertices;
        disk.uv = uv;
        disk.triangles = triangles;
        disk.RecalculateNormals();
        disk.RecalculateBounds();
        return disk;
    }

    private void ApplyVisualState(bool immediate)
    {
        EnsureVisual();

        float fill = reservoir != null ? reservoir.FillPercent : 1f;
        float height = Mathf.Lerp(minLocalY, maxLocalY, Mathf.Clamp01(fill));
        visualObject.transform.localPosition = new Vector3(0f, height, 0f);
        visualObject.transform.localScale = new Vector3(radius, 1f, radius);

        Vector2 targetSlosh = Vector2.zero;
        if (motionDataProvider != null)
        {
            Vector3 localVelocity = motionDataProvider.LocalVelocity;
            targetSlosh = new Vector2(-localVelocity.x, -localVelocity.z) * sloshStrength;
            targetSlosh = Vector2.ClampMagnitude(targetSlosh, 1f);
        }

        smoothedSlosh = immediate
            ? targetSlosh
            : Vector2.SmoothDamp(smoothedSlosh, targetSlosh, ref sloshVelocity, sloshDamping);

        float pitch = Mathf.Clamp(smoothedSlosh.y * maxSloshAngle, -maxSloshAngle, maxSloshAngle);
        float roll = Mathf.Clamp(smoothedSlosh.x * maxSloshAngle, -maxSloshAngle, maxSloshAngle);
        visualObject.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);

        Color paintColor = color;
        paintColor.a = Mathf.Lerp(emptyAlpha, alpha, fill);
        if (material != null)
        {
            material.SetColor(BaseColorId, paintColor);
            material.SetColor(ColorId, paintColor);
        }

        bool visible = fill > 0.001f || emptyAlpha > 0.001f;
        meshRenderer.enabled = visible;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Transform root = bucketRoot != null ? bucketRoot : transform;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = root.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.35f);
        float y = Mathf.Lerp(minLocalY, maxLocalY, reservoir != null ? reservoir.FillPercent : 1f);
        Gizmos.DrawWireSphere(new Vector3(0f, y, 0f), radius);
        Gizmos.matrix = previous;
    }
}
