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
    public float minLocalY = -0.37f;
    public float maxLocalY = -0.035f;
    [Min(0.01f)]
    public float radius = 0.36f;
    public Vector2 localCenterXZ;
    public Color color = new Color(1f, 0.24f, 0.04f, 0.86f);
    [Range(0f, 1f)]
    public float alpha = 0.86f;
    [Range(0f, 1f)]
    public float emptyAlpha = 0.02f;
    public Material material;
    [Min(8)]
    public int segments = 48;

    [Header("Bucket Fit")]
    [Min(0f)]
    public float verticalPaddingFromBottom = 0.08f;
    [Min(0f)]
    public float topPaddingFromRim = 0.16f;
    [Range(0.1f, 1.2f)]
    public float surfaceRadiusScale = 0.88f;
    public bool forceVisibleInDemo = true;
    public float debugRaiseOffset = 0.035f;
    public bool doubleSidedSurface = true;
    public bool showCutawayHelper = false;

    [Header("Volume Illusion")]
    public bool showSurfaceDisk = true;
    public bool showSideVolume = true;
    public bool showMeniscusRing = true;
    [Range(0f, 1f)]
    public float sideVolumeAlpha = 0.36f;
    [Range(0f, 1f)]
    public float meniscusAlpha = 0.92f;

    [Header("Slosh Visual Only")]
    [Range(0f, 1f)]
    public float sloshStrength = 0.18f;
    [Min(0.001f)]
    public float sloshDamping = 0.12f;
    [Range(0f, 20f)]
    public float maxSloshAngle = 9f;
    public bool showDebugGizmos = true;

    private GameObject visualObject;
    private MeshFilter surfaceMeshFilter;
    private MeshRenderer surfaceRenderer;
    private MeshFilter sideMeshFilter;
    private MeshRenderer sideRenderer;
    private MeshFilter meniscusMeshFilter;
    private MeshRenderer meniscusRenderer;
    private Mesh surfaceMesh;
    private Mesh sideMesh;
    private Mesh meniscusMesh;
    private MaterialPropertyBlock propertyBlock;
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
        verticalPaddingFromBottom = Mathf.Max(0f, verticalPaddingFromBottom);
        topPaddingFromRim = Mathf.Max(0f, topPaddingFromRim);
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
            localCenterXZ = new Vector2(bucketCollisionProxy.localCenter.x, bucketCollisionProxy.localCenter.z);
            minLocalY = bucketCollisionProxy.bottomOffset + verticalPaddingFromBottom;
            float rimY = bucketCollisionProxy.localCenter.y + bucketCollisionProxy.height * 0.5f;
            maxLocalY = rimY - topPaddingFromRim;
            radius = Mathf.Max(0.05f, (bucketCollisionProxy.radius - bucketCollisionProxy.wallThickness * 2.5f) * surfaceRadiusScale);
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

        visualObject.transform.SetParent(bucketRoot != null ? bucketRoot : transform, false);
        EnsurePart("SurfaceDisk", ref surfaceMeshFilter, ref surfaceRenderer, ref surfaceMesh, BuildDiskMesh);
        EnsurePart("SideVolume", ref sideMeshFilter, ref sideRenderer, ref sideMesh, BuildSideBandMesh);
        EnsurePart("MeniscusRing", ref meniscusMeshFilter, ref meniscusRenderer, ref meniscusMesh, BuildRingMesh);
        visualObject.SetActive(enabled);

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void EnsurePart(
        string partName,
        ref MeshFilter meshFilter,
        ref MeshRenderer meshRenderer,
        ref Mesh cachedMesh,
        System.Func<Mesh> meshFactory)
    {
        Transform part = visualObject.transform.Find(partName);
        GameObject partObject = part != null ? part.gameObject : new GameObject(partName);
        partObject.transform.SetParent(visualObject.transform, false);

        if (meshFilter == null)
        {
            meshFilter = partObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = partObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = partObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = partObject.AddComponent<MeshRenderer>();
            }
        }

        if (cachedMesh == null || cachedMesh.vertexCount == 0)
        {
            cachedMesh = meshFactory();
            meshFilter.sharedMesh = cachedMesh;
        }

        meshRenderer.sharedMaterial = material;
    }

    private Mesh BuildDiskMesh()
    {
        Mesh disk = new Mesh();
        disk.name = "Bucket Reservoir Surface Visual";

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];
        int triangleCount = segments * 3 * (doubleSidedSurface ? 2 : 1);
        int[] triangles = new int[triangleCount];

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

            if (doubleSidedSurface)
            {
                int backIndex = segments * 3 + triangleIndex;
                triangles[backIndex] = 0;
                triangles[backIndex + 1] = triangles[triangleIndex + 2];
                triangles[backIndex + 2] = triangles[triangleIndex + 1];
            }
        }

        disk.vertices = vertices;
        disk.uv = uv;
        disk.triangles = triangles;
        disk.RecalculateNormals();
        disk.RecalculateBounds();
        return disk;
    }

    private Mesh BuildSideBandMesh()
    {
        Mesh band = new Mesh();
        band.name = "Bucket Reservoir Side Volume Visual";

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6 * 2];

        for (int i = 0; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            int bottom = i * 2;
            int top = bottom + 1;
            vertices[bottom] = new Vector3(x, 0f, z);
            vertices[top] = new Vector3(x, 1f, z);
            uv[bottom] = new Vector2(i / (float)segments, 0f);
            uv[top] = new Vector2(i / (float)segments, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int bottom0 = i * 2;
            int top0 = bottom0 + 1;
            int bottom1 = bottom0 + 2;
            int top1 = bottom0 + 3;
            int triangleIndex = i * 12;

            triangles[triangleIndex] = bottom0;
            triangles[triangleIndex + 1] = top0;
            triangles[triangleIndex + 2] = top1;
            triangles[triangleIndex + 3] = bottom0;
            triangles[triangleIndex + 4] = top1;
            triangles[triangleIndex + 5] = bottom1;

            triangles[triangleIndex + 6] = bottom0;
            triangles[triangleIndex + 7] = top1;
            triangles[triangleIndex + 8] = top0;
            triangles[triangleIndex + 9] = bottom0;
            triangles[triangleIndex + 10] = bottom1;
            triangles[triangleIndex + 11] = top1;
        }

        band.vertices = vertices;
        band.uv = uv;
        band.triangles = triangles;
        band.RecalculateNormals();
        band.RecalculateBounds();
        return band;
    }

    private Mesh BuildRingMesh()
    {
        Mesh ring = new Mesh();
        ring.name = "Bucket Reservoir Meniscus Ring Visual";

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6 * (doubleSidedSurface ? 2 : 1)];
        const float innerRadius = 0.94f;
        const float outerRadius = 1.035f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            int inner = i * 2;
            int outer = inner + 1;
            vertices[inner] = new Vector3(x * innerRadius, 0f, z * innerRadius);
            vertices[outer] = new Vector3(x * outerRadius, 0f, z * outerRadius);
            uv[inner] = new Vector2(0f, i / (float)segments);
            uv[outer] = new Vector2(1f, i / (float)segments);
        }

        for (int i = 0; i < segments; i++)
        {
            int inner0 = i * 2;
            int outer0 = inner0 + 1;
            int inner1 = inner0 + 2;
            int outer1 = inner0 + 3;
            int triangleIndex = i * 6;

            triangles[triangleIndex] = inner0;
            triangles[triangleIndex + 1] = outer0;
            triangles[triangleIndex + 2] = outer1;
            triangles[triangleIndex + 3] = inner0;
            triangles[triangleIndex + 4] = outer1;
            triangles[triangleIndex + 5] = inner1;

            if (doubleSidedSurface)
            {
                int backIndex = segments * 6 + triangleIndex;
                triangles[backIndex] = inner0;
                triangles[backIndex + 1] = outer1;
                triangles[backIndex + 2] = outer0;
                triangles[backIndex + 3] = inner0;
                triangles[backIndex + 4] = inner1;
                triangles[backIndex + 5] = outer1;
            }
        }

        ring.vertices = vertices;
        ring.uv = uv;
        ring.triangles = triangles;
        ring.RecalculateNormals();
        ring.RecalculateBounds();
        return ring;
    }

    private void ApplyVisualState(bool immediate)
    {
        EnsureVisual();

        float fill = reservoir != null ? reservoir.FillPercent : 1f;
        float visibleFill = forceVisibleInDemo ? Mathf.Max(fill, 0.015f) : fill;
        float bottomY = minLocalY;
        float height = Mathf.Lerp(minLocalY, maxLocalY, Mathf.Clamp01(visibleFill)) + debugRaiseOffset;
        float volumeHeight = Mathf.Max(0.001f, height - bottomY);
        Vector3 center = new Vector3(localCenterXZ.x, height, localCenterXZ.y);

        Vector2 targetSlosh = Vector2.zero;
        if (motionDataProvider != null)
        {
            Vector3 localVelocity = motionDataProvider.LocalVelocity;
            Vector3 localAcceleration = bucketRoot != null
                ? bucketRoot.InverseTransformDirection(motionDataProvider.Acceleration)
                : motionDataProvider.Acceleration;
            targetSlosh = new Vector2(
                -localVelocity.x * 0.45f - localAcceleration.x * 0.035f,
                -localVelocity.z * 0.45f - localAcceleration.z * 0.035f) * sloshStrength;
            targetSlosh = Vector2.ClampMagnitude(targetSlosh, 1f);
        }

        smoothedSlosh = immediate
            ? targetSlosh
            : Vector2.SmoothDamp(smoothedSlosh, targetSlosh, ref sloshVelocity, sloshDamping);

        float pitch = Mathf.Clamp(smoothedSlosh.y * maxSloshAngle, -maxSloshAngle, maxSloshAngle);
        float roll = Mathf.Clamp(smoothedSlosh.x * maxSloshAngle, -maxSloshAngle, maxSloshAngle);
        Quaternion sloshRotation = Quaternion.Euler(pitch, 0f, roll);

        Color paintColor = color;
        paintColor.a = Mathf.Lerp(emptyAlpha, alpha, Mathf.Clamp01(fill));
        Color sideColor = paintColor;
        sideColor.a = Mathf.Lerp(0f, sideVolumeAlpha, Mathf.Clamp01(fill));
        Color meniscusColor = Color.Lerp(paintColor, new Color(0.55f, 0.08f, 0.015f, 1f), 0.35f);
        meniscusColor.a = Mathf.Lerp(0f, meniscusAlpha, Mathf.Clamp01(fill));

        bool visible = fill > 0.001f || (forceVisibleInDemo && emptyAlpha > 0.001f);
        ApplyPart(
            surfaceRenderer,
            surfaceMeshFilter != null ? surfaceMeshFilter.transform : null,
            showSurfaceDisk && visible,
            center,
            sloshRotation,
            new Vector3(radius, 1f, radius),
            paintColor);
        ApplyPart(
            sideRenderer,
            sideMeshFilter != null ? sideMeshFilter.transform : null,
            showSideVolume && visible && volumeHeight > 0.002f,
            new Vector3(localCenterXZ.x, bottomY, localCenterXZ.y),
            Quaternion.identity,
            new Vector3(radius, volumeHeight, radius),
            sideColor);
        ApplyPart(
            meniscusRenderer,
            meniscusMeshFilter != null ? meniscusMeshFilter.transform : null,
            showMeniscusRing && visible,
            center + Vector3.up * 0.002f,
            sloshRotation,
            new Vector3(radius, 1f, radius),
            meniscusColor);
    }

    private void ApplyPart(
        MeshRenderer renderer,
        Transform partTransform,
        bool visible,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Color partColor)
    {
        if (renderer == null || partTransform == null)
        {
            return;
        }

        renderer.enabled = visible;
        partTransform.localPosition = localPosition;
        partTransform.localRotation = localRotation;
        partTransform.localScale = localScale;

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, partColor);
        propertyBlock.SetColor(ColorId, partColor);
        renderer.SetPropertyBlock(propertyBlock);
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
        Gizmos.DrawWireSphere(new Vector3(localCenterXZ.x, y + debugRaiseOffset, localCenterXZ.y), radius);
        if (showCutawayHelper)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            Gizmos.DrawLine(new Vector3(localCenterXZ.x - radius, y + debugRaiseOffset, localCenterXZ.y), new Vector3(localCenterXZ.x + radius, y + debugRaiseOffset, localCenterXZ.y));
        }
        Gizmos.matrix = previous;
    }
}
