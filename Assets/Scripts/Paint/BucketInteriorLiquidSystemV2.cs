using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(140)]
public class BucketInteriorLiquidSystemV2 : MonoBehaviour
{
    private const string RuntimeObjectName = "BucketInteriorLiquidSystemV2_Runtime";
    private const string SurfaceObjectName = "InteriorPaint_Surface_V3";
    private const string VolumeObjectName = "InteriorPaint_VolumeBody_V3";
    private const string WetWallObjectName = "InteriorPaint_WetWall_V3";
    private const string MeniscusObjectName = "InteriorPaint_Meniscus_V3";
    private const string RimFoamObjectName = "InteriorPaint_RimHighlights_V3";

    [Header("References")]
    public Transform bucketRoot;
    public PaintEmitter paintEmitter;

    [Header("Master Switches")]
    public bool enableInteriorPaint = true;
    public bool autoFindReferences = true;
    public bool followEmitterColor = true;
    public bool useGravityAlignedSurface = true;
    public bool renderLiquidVolume = false;
    public bool renderMeniscus = true;
    public bool renderWetInnerWall = true;
    public bool renderRimHighlights = true;
    public bool presentationBucketTransparency = false;

    [Header("Professional Auto Fit")]
    [Tooltip("Automatically estimates the bucket interior from the visible bucket body mesh so the liquid sits in the real bucket instead of using guessed values.")]
    public bool autoCalibrateFromBucketBody = true;
    [Tooltip("Re-runs calibration for the first seconds after scene load. Helpful when prefabs are instantiated slightly later.")]
    public bool keepCalibratingAtStartup = true;
    public float startupCalibrationDuration = 2.5f;
    [Range(0.35f, 0.90f)] public float autoInnerRadiusRatio = 0.58f;
    [Range(0.00f, 0.25f)] public float autoBottomPaddingRatio = 0.10f;
    [Range(0.05f, 0.45f)] public float autoRimPaddingRatio = 0.30f;

    [Header("Bucket Interior Geometry")]
    [Tooltip("Uses the pour hole / PaintEmitter as a safety anchor so the internal liquid cannot be calibrated above the real bucket body when the imported mesh contains handles or decorative geometry.")]
    public bool constrainToNozzleAnchor = true;
    [Range(0.02f, 0.30f)] public float nozzleBottomOffset = 0.075f;
    [Range(0.30f, 1.25f)] public float maxLiquidHeightAboveNozzle = 0.72f;
    [Range(0.35f, 0.96f)] public float maximumVisualFillHeightRatio = 0.92f;

    [Tooltip("Center of the visible liquid in Bucket local XZ coordinates. Auto-fit aligns it to the real bucket/nozzle center.")]
    public Vector2 liquidCenterLocalXZ = Vector2.zero;
    [Tooltip("Interior radius in Bucket local units. Auto-fit normally sets this.")]
    public float innerRadius = 0.455f;
    [Tooltip("Bottom of the visible paint volume in Bucket local units. Auto-fit normally sets this.")]
    public float bottomLocalY = -0.78f;
    [Tooltip("Maximum liquid height / safe rim height in Bucket local units. Auto-fit normally sets this.")]
    public float rimLocalY = 0.70f;
    public float rimClearance = 0.030f;
    public float bottomClearance = 0.025f;
    [Range(0.15f, 1f)] public float nearEmptyRadiusScale = 0.55f;
    [Range(0f, 0.12f)] public float surfaceInset = 0.018f;
    [Range(0f, 0.12f)] public float sideInset = 0.025f;
    [Range(0f, 0.08f)] public float meniscusWidth = 0.032f;
    [Range(0f, 0.08f)] public float meniscusHeight = 0.030f;

    [Header("Rendering Quality")]
    [Range(32, 224)] public int radialSegments = 96;
    [Range(8, 72)] public int radialRings = 24;
    [Range(2, 28)] public int sideVerticalSubdivisions = 10;
    [Range(0, 64)] public int rimHighlightCount = 18;
    [Tooltip("Minimum time between interior-liquid mesh rebuilds. Higher values reduce CPU load while keeping the liquid interactive.")]
    [Range(0.02f, 0.18f)] public float meshRebuildInterval = 0.055f;

    [Header("Liquid Visual Material")]
    public Color paintColor = new Color(0.95f, 0.04f, 0.01f, 1f);
    [Range(0.1f, 1f)] public float surfaceAlpha = 0.985f;
    [Range(0.0f, 1f)] public float volumeAlpha = 0.72f;
    [Range(0.0f, 1f)] public float wetWallAlpha = 0.62f;
    [Range(0.0f, 1f)] public float meniscusAlpha = 1.0f;
    [Range(0f, 1f)] public float smoothness = 0.96f;
    [Range(0f, 3f)] public float specularBoost = 2.1f;
    [Range(0f, 1f)] public float darkenWithDepth = 0.42f;
    [Range(0f, 1f)] public float bucketTransparencyAlpha = 0.38f;
    [Range(0f, 1f)] public float paintEmissionBoost = 0.08f;

    [Header("Interactive Slosh Model")]
    public bool enableSlosh = true;
    [Range(0f, 2.5f)] public float sloshStrength = 0.82f;
    [Range(0f, 2.0f)] public float surfaceTiltResponse = 0.92f;
    [Range(0f, 0.30f)] public float sloshCenterShift = 0.105f;
    [Range(0.01f, 1.5f)] public float sloshResponseTime = 0.095f;
    [Range(0f, 14f)] public float sloshDamping = 6.8f;
    [Range(0f, 0.12f)] public float waveAmplitude = 0.044f;
    [Range(0f, 18f)] public float waveFrequency = 8.4f;
    [Range(0f, 5f)] public float rimWaveMultiplier = 2.25f;
    [Range(0f, 3f)] public float angularVelocitySlosh = 1.2f;

    [Header("Runtime Readout")]
    [SerializeField] private float fill01 = 1f;
    [SerializeField] private float surfaceLocalY;
    [SerializeField] private float sloshIntensity;
    [SerializeField] private float visibleLiters;
    [SerializeField] private float estimatedSurfaceArea;
    [SerializeField] private string calibrationStatus = "manual";

    private GameObject surfaceObject;
    private GameObject volumeObject;
    private GameObject wetWallObject;
    private GameObject meniscusObject;
    private GameObject rimHighlightObject;

    private Mesh surfaceMesh;
    private Mesh volumeMesh;
    private Mesh wetWallMesh;
    private Mesh meniscusMesh;
    private Mesh rimHighlightMesh;

    private Material surfaceMaterial;
    private Material volumeMaterial;
    private Material wetWallMaterial;
    private Material meniscusMaterial;
    private Material rimHighlightMaterial;

    private Vector3 lastBucketWorldPosition;
    private Quaternion lastBucketRotation;
    private Vector3 lastBucketVelocity;
    private bool hasLastMotion;
    private Vector3 filteredAccelerationWorld;
    private Vector3 sloshVelocityWorld;
    private Vector3 sloshNormalWorld = Vector3.up;
    private Vector3 sloshOffsetLocal;
    private float rebuildTimer;
    private float startupTimer;
    private int cachedSegments;
    private int cachedRings;
    private int cachedSideSubdivisions;
    private int cachedRimHighlights;
    private readonly List<Renderer> bucketRenderers = new List<Renderer>();
    private readonly Dictionary<Material, Color> originalBucketColors = new Dictionary<Material, Color>();
    private bool bucketTransparencyApplied;
    private bool calibratedOnce;
    private bool forceRebuildRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateRuntimeInstance()
    {
        if (Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<BucketInteriorLiquidSystemV2>();
    }

    private void Awake()
    {
        AutoFindReferencesNow();
        TryAutoCalibrateGeometry(true);
        EnsureLiquidObjects();
    }

    private void Start()
    {
        AutoFindReferencesNow();
        TryAutoCalibrateGeometry(true);
        EnsureLiquidObjects();
        CaptureBucketRenderers();
        ApplyBucketTransparencyIfNeeded(true);
        ForceRebuild();
    }

    private void LateUpdate()
    {
        if (autoFindReferences)
        {
            AutoFindReferencesNow();
        }

        if (keepCalibratingAtStartup && startupTimer < startupCalibrationDuration)
        {
            startupTimer += Time.deltaTime;
            TryAutoCalibrateGeometry(false);
        }

        EnsureLiquidObjects();

        if (bucketRoot == null)
        {
            SetLiquidObjectsActive(false);
            return;
        }

        UpdateFillFromEmitter();
        UpdateSloshPhysics(Time.deltaTime);
        UpdateMaterials();
        ApplyBucketTransparencyIfNeeded(false);

        bool shouldRender = enableInteriorPaint && fill01 > 0.002f;
        SetLiquidObjectsActive(shouldRender);

        if (!shouldRender)
        {
            return;
        }

        rebuildTimer += Time.deltaTime;
        bool topologyChanged = cachedSegments != radialSegments || cachedRings != radialRings || cachedSideSubdivisions != sideVerticalSubdivisions || cachedRimHighlights != rimHighlightCount;
        if (forceRebuildRequested || topologyChanged || rebuildTimer >= Mathf.Max(0.02f, meshRebuildInterval))
        {
            forceRebuildRequested = false;
            rebuildTimer = 0f;
            RebuildAllMeshes();
        }
    }

    private void AutoFindReferencesNow()
    {
        if (paintEmitter == null)
        {
            paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (bucketRoot == null)
        {
            GameObject bucketObject = GameObject.Find("Bucket");
            if (bucketObject != null)
            {
                bucketRoot = bucketObject.transform;
            }
        }

        if (bucketRoot == null && paintEmitter != null && paintEmitter.transform.parent != null)
        {
            bucketRoot = paintEmitter.transform.parent;
        }
    }

    private void TryAutoCalibrateGeometry(bool force)
    {
        if (!autoCalibrateFromBucketBody || bucketRoot == null)
        {
            return;
        }

        if (calibratedOnce && !force)
        {
            return;
        }

        Renderer[] renderers = bucketRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        bool hasBounds = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        List<float> sampleY = new List<float>(4096);
        List<float> sampleRadius = new List<float>(4096);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !IsBucketBodyRenderer(renderer))
            {
                continue;
            }

            MeshFilter mf = renderer.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;
                Bounds b = mesh.bounds;
                AddLocalBoundsCornersToAggregate(renderer.transform, b, ref min, ref max, ref hasBounds);

                // Some imported production meshes are intentionally non-readable to save memory.
                // Reading mesh.vertices on those assets throws a runtime UnityException and breaks Play Mode.
                // Use exact vertex samples only when the import settings allow it; otherwise fall back to
                // renderer bounds samples so calibration remains stable without requiring Read/Write.
                if (mesh.isReadable)
                {
                    AddMeshVertexSamples(renderer.transform, mesh, sampleY, sampleRadius, 4096);
                }
                else
                {
                    AddRendererBoundsSamples(renderer.bounds, sampleY, sampleRadius);
                }
            }
            else
            {
                Bounds b = renderer.bounds;
                AddWorldBoundsCornersToAggregate(b, ref min, ref max, ref hasBounds);
                AddRendererBoundsSamples(b, sampleY, sampleRadius);
            }
        }

        if (!hasBounds)
        {
            calibrationStatus = "no body renderer";
            return;
        }

        // Use robust percentiles instead of raw bounds. Imported bucket meshes often include lips,
        // decorative handles, or hidden helper geometry that made the paint appear above the bucket.
        float lowY = sampleY.Count >= 8 ? Percentile(sampleY, 0.08f) : min.y;
        float highY = sampleY.Count >= 8 ? Percentile(sampleY, 0.76f) : max.y - (max.y - min.y) * autoRimPaddingRatio;
        float rawHeight = Mathf.Max(0.08f, max.y - min.y);
        float bodyHeight = Mathf.Clamp(highY - lowY, rawHeight * 0.35f, rawHeight * 0.92f);

        float boundsRadiusX = Mathf.Max(0.04f, (max.x - min.x) * 0.5f);
        float boundsRadiusZ = Mathf.Max(0.04f, (max.z - min.z) * 0.5f);
        float outerRadius = Mathf.Max(0.08f, Mathf.Min(boundsRadiusX, boundsRadiusZ));

        liquidCenterLocalXZ = new Vector2((min.x + max.x) * 0.5f, (min.z + max.z) * 0.5f);
        if (constrainToNozzleAnchor && paintEmitter != null)
        {
            Vector3 nozzleLocal = bucketRoot.InverseTransformPoint(paintEmitter.transform.position);
            liquidCenterLocalXZ = new Vector2(nozzleLocal.x, nozzleLocal.z);
        }

        innerRadius = Mathf.Clamp(outerRadius * autoInnerRadiusRatio, 0.08f, outerRadius * 0.82f);
        bottomLocalY = lowY + bodyHeight * autoBottomPaddingRatio;
        rimLocalY = lowY + bodyHeight * maximumVisualFillHeightRatio;

        ApplyNozzleAnchorConstraint(rawHeight);
        ValidateLiquidGeometry();

        calibratedOnce = true;
        calibrationStatus = constrainToNozzleAnchor && paintEmitter != null ? "auto-fit + nozzle clamp" : "robust auto-fit";
    }

    private bool IsBucketBodyRenderer(Renderer renderer)
    {
        string n = renderer.gameObject.name.ToLowerInvariant();
        if (n.Contains("interiorpaint") || n.Contains("paintnozzle") || n.Contains("rope") || n.Contains("hand") || n.Contains("handle") || n.Contains("canvas"))
        {
            return false;
        }

        if (n.Contains("bucket") || n.Contains("body") || n.Contains("model"))
        {
            return true;
        }

        return renderer.transform.IsChildOf(bucketRoot) && renderer.GetComponent<MeshFilter>() != null;
    }

    private void AddLocalBoundsCornersToAggregate(Transform sourceTransform, Bounds bounds, ref Vector3 min, ref Vector3 max, ref bool hasBounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 local = c + Vector3.Scale(e, new Vector3(x, y, z));
                    Vector3 bucketLocal = bucketRoot.InverseTransformPoint(sourceTransform.TransformPoint(local));
                    min = Vector3.Min(min, bucketLocal);
                    max = Vector3.Max(max, bucketLocal);
                    hasBounds = true;
                }
            }
        }
    }

    private void AddWorldBoundsCornersToAggregate(Bounds bounds, ref Vector3 min, ref Vector3 max, ref bool hasBounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 world = c + Vector3.Scale(e, new Vector3(x, y, z));
                    Vector3 bucketLocal = bucketRoot.InverseTransformPoint(world);
                    min = Vector3.Min(min, bucketLocal);
                    max = Vector3.Max(max, bucketLocal);
                    hasBounds = true;
                }
            }
        }
    }

    private void AddMeshVertexSamples(Transform sourceTransform, Mesh mesh, List<float> sampleY, List<float> sampleRadius, int maxSamples)
    {
        if (bucketRoot == null || sourceTransform == null || mesh == null || mesh.vertexCount == 0)
        {
            return;
        }

        if (!mesh.isReadable)
        {
            return;
        }

        Vector3[] vertices;
        try
        {
            vertices = mesh.vertices;
        }
        catch (UnityException)
        {
            return;
        }

        int step = Mathf.Max(1, vertices.Length / Mathf.Max(1, maxSamples));
        for (int i = 0; i < vertices.Length; i += step)
        {
            Vector3 bucketLocal = bucketRoot.InverseTransformPoint(sourceTransform.TransformPoint(vertices[i]));
            float radius = new Vector2(bucketLocal.x, bucketLocal.z).magnitude;
            if (radius <= 0.0001f)
            {
                continue;
            }

            sampleY.Add(bucketLocal.y);
            sampleRadius.Add(radius);
        }
    }

    private void AddRendererBoundsSamples(Bounds bounds, List<float> sampleY, List<float> sampleRadius)
    {
        if (bucketRoot == null)
        {
            return;
        }

        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        for (int x = -1; x <= 1; x += 1)
        {
            for (int y = -1; y <= 1; y += 1)
            {
                for (int z = -1; z <= 1; z += 1)
                {
                    Vector3 world = c + Vector3.Scale(e, new Vector3(x, y, z));
                    Vector3 local = bucketRoot.InverseTransformPoint(world);
                    sampleY.Add(local.y);
                    sampleRadius.Add(new Vector2(local.x, local.z).magnitude);
                }
            }
        }
    }

    private float Percentile(List<float> values, float percentile01)
    {
        if (values == null || values.Count == 0)
        {
            return 0f;
        }

        values.Sort();
        float f = Mathf.Clamp01(percentile01) * (values.Count - 1);
        int low = Mathf.FloorToInt(f);
        int high = Mathf.Min(values.Count - 1, low + 1);
        return Mathf.Lerp(values[low], values[high], f - low);
    }

    private void ApplyNozzleAnchorConstraint(float rawHeight)
    {
        if (!constrainToNozzleAnchor || paintEmitter == null || bucketRoot == null)
        {
            return;
        }

        Vector3 nozzleLocal = bucketRoot.InverseTransformPoint(paintEmitter.transform.position);
        float safeBottom = nozzleLocal.y + nozzleBottomOffset;
        float safeHeight = Mathf.Clamp(maxLiquidHeightAboveNozzle, Mathf.Max(0.22f, rawHeight * 0.28f), Mathf.Max(0.32f, rawHeight * 0.78f));
        float safeRim = safeBottom + safeHeight;

        bottomLocalY = Mathf.Max(bottomLocalY, safeBottom);
        rimLocalY = Mathf.Min(rimLocalY, safeRim);
    }

    private void ValidateLiquidGeometry()
    {
        if (rimLocalY <= bottomLocalY + 0.18f)
        {
            rimLocalY = bottomLocalY + 0.42f;
        }

        liquidCenterLocalXZ.x = Mathf.Clamp(liquidCenterLocalXZ.x, -0.75f, 0.75f);
        liquidCenterLocalXZ.y = Mathf.Clamp(liquidCenterLocalXZ.y, -0.75f, 0.75f);
        innerRadius = Mathf.Clamp(innerRadius, 0.10f, 0.52f);
        surfaceInset = Mathf.Clamp(surfaceInset, 0.02f, Mathf.Max(0.02f, innerRadius * 0.18f));
        sideInset = Mathf.Clamp(sideInset, 0.03f, Mathf.Max(0.03f, innerRadius * 0.22f));
        radialSegments = Mathf.Clamp(radialSegments, 32, 224);
        radialRings = Mathf.Clamp(radialRings, 8, 72);
        sideVerticalSubdivisions = Mathf.Clamp(sideVerticalSubdivisions, 2, 28);
        rimHighlightCount = Mathf.Clamp(rimHighlightCount, 0, 64);
        meshRebuildInterval = Mathf.Clamp(meshRebuildInterval, 0.02f, 0.18f);
    }

    private void EnsureLiquidObjects()
    {
        if (bucketRoot == null)
        {
            return;
        }

        if (surfaceObject == null)
        {
            surfaceObject = CreateChildMeshObject(SurfaceObjectName, out surfaceMesh, out MeshRenderer renderer);
            surfaceMaterial = CreateLiquidMaterial("MAT_Runtime_BucketPaint_DeepGlossSurface", surfaceAlpha, true, true);
            renderer.sharedMaterial = surfaceMaterial;
        }

        if (volumeObject == null)
        {
            volumeObject = CreateChildMeshObject(VolumeObjectName, out volumeMesh, out MeshRenderer renderer);
            volumeMaterial = CreateLiquidMaterial("MAT_Runtime_BucketPaint_VolumetricBody", volumeAlpha, true, false);
            renderer.sharedMaterial = volumeMaterial;
        }

        if (wetWallObject == null)
        {
            wetWallObject = CreateChildMeshObject(WetWallObjectName, out wetWallMesh, out MeshRenderer renderer);
            wetWallMaterial = CreateLiquidMaterial("MAT_Runtime_BucketPaint_WetInnerWall", wetWallAlpha, true, false);
            renderer.sharedMaterial = wetWallMaterial;
        }

        if (meniscusObject == null)
        {
            meniscusObject = CreateChildMeshObject(MeniscusObjectName, out meniscusMesh, out MeshRenderer renderer);
            meniscusMaterial = CreateLiquidMaterial("MAT_Runtime_BucketPaint_ThickMeniscus", meniscusAlpha, true, true);
            renderer.sharedMaterial = meniscusMaterial;
        }

        if (rimHighlightObject == null)
        {
            rimHighlightObject = CreateChildMeshObject(RimFoamObjectName, out rimHighlightMesh, out MeshRenderer renderer);
            rimHighlightMaterial = CreateLiquidMaterial("MAT_Runtime_BucketPaint_SpecularRimDetails", 0.78f, true, true);
            renderer.sharedMaterial = rimHighlightMaterial;
        }

        ParentToBucket(surfaceObject);
        ParentToBucket(volumeObject);
        ParentToBucket(wetWallObject);
        ParentToBucket(meniscusObject);
        ParentToBucket(rimHighlightObject);
    }

    private GameObject CreateChildMeshObject(string objectName, out Mesh mesh, out MeshRenderer meshRenderer)
    {
        Transform existing = bucketRoot != null ? bucketRoot.Find(objectName) : null;
        GameObject obj = existing != null ? existing.gameObject : new GameObject(objectName);
        ParentToBucket(obj);

        MeshFilter filter = obj.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = obj.AddComponent<MeshFilter>();
        }

        meshRenderer = obj.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = obj.AddComponent<MeshRenderer>();
        }

        mesh = filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = objectName + "_Mesh";
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
        meshRenderer.allowOcclusionWhenDynamic = false;
        return obj;
    }

    private void ParentToBucket(GameObject obj)
    {
        if (obj == null || bucketRoot == null)
        {
            return;
        }

        if (obj.transform.parent != bucketRoot)
        {
            obj.transform.SetParent(bucketRoot, false);
        }

        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
    }

    private Material CreateLiquidMaterial(string materialName, float alpha, bool transparent, bool glossy)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.name = materialName;
        ConfigureLiquidMaterial(material, alpha, transparent, glossy);
        return material;
    }

    private void ConfigureLiquidMaterial(Material material, float alpha, bool transparent, bool glossy)
    {
        if (material == null)
        {
            return;
        }

        Color color = GetEffectivePaintColor(alpha);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", glossy ? smoothness : smoothness * 0.82f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", glossy ? smoothness : smoothness * 0.82f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", Color.Lerp(Color.black, Color.white, Mathf.Clamp01(specularBoost * 0.35f)));
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * paintEmissionBoost);

        if (paintEmissionBoost > 0.001f)
        {
            material.EnableKeyword("_EMISSION");
        }

        if (transparent)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent + 60;
        }
        else
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            material.renderQueue = (int)RenderQueue.Geometry + 20;
        }
    }

    private Color GetEffectivePaintColor(float alpha)
    {
        Color c = followEmitterColor && paintEmitter != null ? paintEmitter.paintColor : paintColor;
        float depth = Mathf.Clamp01(fill01);
        Color deep = Color.Lerp(c, Color.black, darkenWithDepth);
        c = Color.Lerp(c, deep, depth * 0.66f);
        c.a = alpha;
        return c;
    }

    private void UpdateFillFromEmitter()
    {
        if (paintEmitter != null)
        {
            fill01 = paintEmitter.PaintFill01;
            visibleLiters = paintEmitter.RemainingPaintAmount;
        }
        else
        {
            fill01 = Mathf.Clamp01(fill01);
            visibleLiters = fill01 * 5f;
        }

        ValidateLiquidGeometry();
        float bottom = bottomLocalY + Mathf.Max(0f, bottomClearance);
        float top = rimLocalY - Mathf.Max(0f, rimClearance);
        if (top <= bottom + 0.05f)
        {
            top = bottom + 0.05f;
        }
        float safeFill = Mathf.Clamp01(fill01);
        surfaceLocalY = Mathf.Lerp(bottom, top, safeFill);
    }

    private void UpdateSloshPhysics(float dt)
    {
        if (bucketRoot == null || !enableSlosh || dt <= 0f)
        {
            filteredAccelerationWorld = Vector3.zero;
            sloshIntensity = 0f;
            sloshNormalWorld = Vector3.up;
            sloshOffsetLocal = Vector3.zero;
            return;
        }

        Vector3 currentPosition = bucketRoot.position;
        Quaternion currentRotation = bucketRoot.rotation;

        if (!hasLastMotion)
        {
            hasLastMotion = true;
            lastBucketWorldPosition = currentPosition;
            lastBucketVelocity = Vector3.zero;
            lastBucketRotation = currentRotation;
            return;
        }

        Vector3 velocity = (currentPosition - lastBucketWorldPosition) / Mathf.Max(dt, 0.0001f);
        Vector3 acceleration = (velocity - lastBucketVelocity) / Mathf.Max(dt, 0.0001f);
        lastBucketWorldPosition = currentPosition;
        lastBucketVelocity = velocity;

        Quaternion delta = currentRotation * Quaternion.Inverse(lastBucketRotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 angularVelocity = axis.sqrMagnitude > 0.001f ? axis.normalized * (angleDeg * Mathf.Deg2Rad / Mathf.Max(dt, 0.0001f)) : Vector3.zero;
        lastBucketRotation = currentRotation;

        Vector3 lateralAcceleration = Vector3.ProjectOnPlane(acceleration, Vector3.up);
        Vector3 angularPush = Vector3.Cross(angularVelocity, Vector3.up) * angularVelocitySlosh;
        Vector3 drive = lateralAcceleration + angularPush;

        float response = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, sloshResponseTime));
        filteredAccelerationWorld = Vector3.Lerp(filteredAccelerationWorld, drive, response);
        filteredAccelerationWorld *= Mathf.Exp(-sloshDamping * dt * 0.11f);

        Vector3 effectiveGravity = Vector3.up - filteredAccelerationWorld * surfaceTiltResponse * 0.055f;
        if (effectiveGravity.sqrMagnitude < 0.001f)
        {
            effectiveGravity = Vector3.up;
        }
        effectiveGravity.Normalize();

        sloshNormalWorld = Vector3.SmoothDamp(sloshNormalWorld, effectiveGravity, ref sloshVelocityWorld, Mathf.Max(0.02f, sloshResponseTime), Mathf.Infinity, dt).normalized;

        float accelerationMagnitude = Mathf.Clamp01(filteredAccelerationWorld.magnitude * sloshStrength * 0.055f);
        sloshIntensity = Mathf.Lerp(sloshIntensity, accelerationMagnitude, response);

        Vector3 offsetWorld = -filteredAccelerationWorld * sloshCenterShift * 0.055f;
        offsetWorld = Vector3.ClampMagnitude(offsetWorld, sloshCenterShift);
        sloshOffsetLocal = bucketRoot.InverseTransformDirection(offsetWorld);
    }

    private void UpdateMaterials()
    {
        if (followEmitterColor && paintEmitter != null)
        {
            paintColor = paintEmitter.paintColor;
        }

        ConfigureLiquidMaterial(surfaceMaterial, surfaceAlpha, true, true);
        ConfigureLiquidMaterial(volumeMaterial, volumeAlpha, true, false);
        ConfigureLiquidMaterial(wetWallMaterial, wetWallAlpha, true, false);
        ConfigureLiquidMaterial(meniscusMaterial, meniscusAlpha, true, true);
        ConfigureLiquidMaterial(rimHighlightMaterial, 0.78f, true, true);
    }

    private void SetLiquidObjectsActive(bool active)
    {
        if (surfaceObject != null) surfaceObject.SetActive(active);
        if (volumeObject != null) volumeObject.SetActive(active && renderLiquidVolume);
        if (wetWallObject != null) wetWallObject.SetActive(active && renderWetInnerWall);
        if (meniscusObject != null) meniscusObject.SetActive(active && renderMeniscus);
        if (rimHighlightObject != null) rimHighlightObject.SetActive(active && renderRimHighlights && rimHighlightCount > 0);
    }

    private void RebuildAllMeshes()
    {
        cachedSegments = radialSegments;
        cachedRings = radialRings;
        cachedSideSubdivisions = sideVerticalSubdivisions;
        cachedRimHighlights = rimHighlightCount;

        RebuildSurfaceMesh();
        if (renderLiquidVolume) RebuildVolumeMesh();
        if (renderWetInnerWall) RebuildWetWallMesh();
        if (renderMeniscus) RebuildMeniscusMesh();
        if (renderRimHighlights) RebuildRimHighlightMesh();
    }

    private Vector3 GetSurfaceNormalLocal()
    {
        if (!useGravityAlignedSurface || bucketRoot == null)
        {
            return Vector3.up;
        }

        Vector3 normal = bucketRoot.InverseTransformDirection(sloshNormalWorld.sqrMagnitude > 0.001f ? sloshNormalWorld : Vector3.up);
        if (normal.y < 0f) normal = -normal;
        if (Mathf.Abs(normal.y) < 0.18f)
        {
            normal.y = 0.18f;
            normal.Normalize();
        }
        return normal.normalized;
    }

    private float GetSurfaceYAt(Vector3 normalLocal, float x, float z, float normalizedRadius, float angle01)
    {
        float y = surfaceLocalY;
        if (Mathf.Abs(normalLocal.y) > 0.0001f)
        {
            y = (surfaceLocalY - normalLocal.x * x - normalLocal.z * z) / normalLocal.y;
        }

        if (enableSlosh && waveAmplitude > 0f)
        {
            float t = Time.time * waveFrequency;
            float rimWeight = Mathf.Lerp(0.15f, rimWaveMultiplier, normalizedRadius * normalizedRadius);
            float choppy = Mathf.Sin(angle01 * Mathf.PI * 5.0f + t) * 0.70f;
            choppy += Mathf.Sin(angle01 * Mathf.PI * 9.0f - t * 0.61f) * 0.34f;
            choppy += Mathf.Sin((x * 11.0f + z * 7.0f) + t * 1.33f) * 0.18f;
            y += choppy * waveAmplitude * sloshIntensity * rimWeight;
        }

        return Mathf.Clamp(y, bottomLocalY + 0.010f, rimLocalY - 0.004f);
    }

    private float GetCurrentSurfaceRadius()
    {
        float fullRadius = Mathf.Max(0.02f, innerRadius - surfaceInset);
        float lowFill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.015f, 0.16f, fill01));
        return fullRadius * Mathf.Lerp(nearEmptyRadiusScale, 1f, lowFill);
    }

    private void RebuildSurfaceMesh()
    {
        if (surfaceMesh == null) return;

        int seg = Mathf.Clamp(radialSegments, 32, 224);
        int rings = Mathf.Clamp(radialRings, 8, 72);
        float radius = GetCurrentSurfaceRadius();
        Vector3 normalLocal = GetSurfaceNormalLocal();

        int vertexCount = (rings + 1) * (seg + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[rings * seg * 6];

        int index = 0;
        for (int r = 0; r <= rings; r++)
        {
            float rn = r / (float)rings;
            float rr = radius * rn;
            float edgeCup = Mathf.Pow(rn, 5f) * meniscusHeight * Mathf.Clamp01(fill01) * 0.35f;

            for (int s = 0; s <= seg; s++)
            {
                float angle01 = s / (float)seg;
                float angle = angle01 * Mathf.PI * 2f;
                float x = liquidCenterLocalXZ.x + Mathf.Cos(angle) * rr + sloshOffsetLocal.x * (1f - rn * 0.28f);
                float z = liquidCenterLocalXZ.y + Mathf.Sin(angle) * rr + sloshOffsetLocal.z * (1f - rn * 0.28f);
                float y = GetSurfaceYAt(normalLocal, x, z, rn, angle01) + edgeCup;

                vertices[index] = new Vector3(x, y, z);
                normals[index] = normalLocal;
                uvs[index] = new Vector2(0.5f + Mathf.Cos(angle) * rn * 0.5f, 0.5f + Mathf.Sin(angle) * rn * 0.5f);
                index++;
            }
        }

        int ti = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a = r * (seg + 1) + s;
                int b = a + 1;
                int c = (r + 1) * (seg + 1) + s;
                int d = c + 1;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        surfaceMesh.Clear(false);
        surfaceMesh.vertices = vertices;
        surfaceMesh.normals = normals;
        surfaceMesh.uv = uvs;
        surfaceMesh.triangles = triangles;
        surfaceMesh.RecalculateTangents();
        surfaceMesh.RecalculateBounds();

        estimatedSurfaceArea = Mathf.PI * radius * radius;
    }

    private void RebuildVolumeMesh()
    {
        if (volumeMesh == null) return;

        int seg = Mathf.Clamp(radialSegments, 32, 224);
        int vertical = Mathf.Clamp(sideVerticalSubdivisions, 2, 28);
        float radius = Mathf.Max(0.02f, innerRadius - sideInset);
        Vector3 normalLocal = GetSurfaceNormalLocal();

        int sideVertexCount = (vertical + 1) * (seg + 1);
        int bottomCenter = sideVertexCount;
        int bottomRingStart = bottomCenter + 1;
        int vertexCount = sideVertexCount + 1 + (seg + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[vertical * seg * 6 + seg * 3];

        int vi = 0;
        for (int yStep = 0; yStep <= vertical; yStep++)
        {
            float yn = yStep / (float)vertical;
            for (int s = 0; s <= seg; s++)
            {
                float angle01 = s / (float)seg;
                float angle = angle01 * Mathf.PI * 2f;
                float x = liquidCenterLocalXZ.x + Mathf.Cos(angle) * radius;
                float z = liquidCenterLocalXZ.y + Mathf.Sin(angle) * radius;
                float topY = GetSurfaceYAt(normalLocal, x, z, 1f, angle01);
                float y = Mathf.Lerp(bottomLocalY + 0.012f, topY, yn);
                vertices[vi] = new Vector3(x, y, z);
                normals[vi] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                uvs[vi] = new Vector2(angle01, yn);
                vi++;
            }
        }

        vertices[bottomCenter] = new Vector3(liquidCenterLocalXZ.x, bottomLocalY + 0.012f, liquidCenterLocalXZ.y);
        normals[bottomCenter] = Vector3.down;
        uvs[bottomCenter] = new Vector2(0.5f, 0.5f);
        for (int s = 0; s <= seg; s++)
        {
            float angle01 = s / (float)seg;
            float angle = angle01 * Mathf.PI * 2f;
            vertices[bottomRingStart + s] = new Vector3(liquidCenterLocalXZ.x + Mathf.Cos(angle) * radius, bottomLocalY + 0.012f, liquidCenterLocalXZ.y + Mathf.Sin(angle) * radius);
            normals[bottomRingStart + s] = Vector3.down;
            uvs[bottomRingStart + s] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
        }

        int ti = 0;
        for (int yStep = 0; yStep < vertical; yStep++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a = yStep * (seg + 1) + s;
                int b = a + 1;
                int c = (yStep + 1) * (seg + 1) + s;
                int d = c + 1;
                triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                triangles[ti++] = b; triangles[ti++] = d; triangles[ti++] = c;
            }
        }

        for (int s = 0; s < seg; s++)
        {
            triangles[ti++] = bottomCenter;
            triangles[ti++] = bottomRingStart + s + 1;
            triangles[ti++] = bottomRingStart + s;
        }

        volumeMesh.Clear(false);
        volumeMesh.vertices = vertices;
        volumeMesh.normals = normals;
        volumeMesh.uv = uvs;
        volumeMesh.triangles = triangles;
        volumeMesh.RecalculateBounds();
    }

    private void RebuildWetWallMesh()
    {
        if (wetWallMesh == null) return;

        int seg = Mathf.Clamp(radialSegments, 32, 224);
        int vertical = Mathf.Clamp(sideVerticalSubdivisions, 2, 28);
        float radius = Mathf.Max(0.02f, innerRadius - sideInset * 0.35f);
        Vector3 normalLocal = GetSurfaceNormalLocal();

        Vector3[] vertices = new Vector3[(vertical + 1) * (seg + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[vertical * seg * 6];

        int vi = 0;
        for (int yStep = 0; yStep <= vertical; yStep++)
        {
            float yn = yStep / (float)vertical;
            for (int s = 0; s <= seg; s++)
            {
                float angle01 = s / (float)seg;
                float angle = angle01 * Mathf.PI * 2f;
                float x = liquidCenterLocalXZ.x + Mathf.Cos(angle) * radius;
                float z = liquidCenterLocalXZ.y + Mathf.Sin(angle) * radius;
                float surfaceY = GetSurfaceYAt(normalLocal, x, z, 1f, angle01) + meniscusHeight * 0.35f;
                float y = Mathf.Lerp(bottomLocalY + 0.015f, surfaceY, yn);
                vertices[vi] = new Vector3(x, y, z);
                normals[vi] = new Vector3(-Mathf.Cos(angle), 0f, -Mathf.Sin(angle)).normalized;
                uvs[vi] = new Vector2(angle01, yn);
                vi++;
            }
        }

        int ti = 0;
        for (int yStep = 0; yStep < vertical; yStep++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a = yStep * (seg + 1) + s;
                int b = a + 1;
                int c = (yStep + 1) * (seg + 1) + s;
                int d = c + 1;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        wetWallMesh.Clear(false);
        wetWallMesh.vertices = vertices;
        wetWallMesh.normals = normals;
        wetWallMesh.uv = uvs;
        wetWallMesh.triangles = triangles;
        wetWallMesh.RecalculateBounds();
    }

    private void RebuildMeniscusMesh()
    {
        if (meniscusMesh == null) return;

        int seg = Mathf.Clamp(radialSegments, 32, 224);
        int bands = 5;
        float radiusOuter = Mathf.Max(0.02f, innerRadius + meniscusWidth * 0.15f);
        float radiusInner = Mathf.Max(0.02f, innerRadius - meniscusWidth * 1.35f);
        Vector3 normalLocal = GetSurfaceNormalLocal();

        Vector3[] vertices = new Vector3[(bands + 1) * (seg + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[bands * seg * 6];

        int vi = 0;
        for (int b = 0; b <= bands; b++)
        {
            float bn = b / (float)bands;
            float radius = Mathf.Lerp(radiusOuter, radiusInner, bn);
            float raisedLip = Mathf.Sin(bn * Mathf.PI) * meniscusHeight * Mathf.Clamp01(fill01);
            for (int s = 0; s <= seg; s++)
            {
                float angle01 = s / (float)seg;
                float angle = angle01 * Mathf.PI * 2f;
                float x = liquidCenterLocalXZ.x + Mathf.Cos(angle) * radius;
                float z = liquidCenterLocalXZ.y + Mathf.Sin(angle) * radius;
                float y = GetSurfaceYAt(normalLocal, x, z, 1f, angle01) + raisedLip;
                vertices[vi] = new Vector3(x, y, z);
                normals[vi] = normalLocal;
                uvs[vi] = new Vector2(angle01, bn);
                vi++;
            }
        }

        int ti = 0;
        for (int b = 0; b < bands; b++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a = b * (seg + 1) + s;
                int bb = a + 1;
                int c = (b + 1) * (seg + 1) + s;
                int d = c + 1;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = bb;
                triangles[ti++] = bb; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        meniscusMesh.Clear(false);
        meniscusMesh.vertices = vertices;
        meniscusMesh.normals = normals;
        meniscusMesh.uv = uvs;
        meniscusMesh.triangles = triangles;
        meniscusMesh.RecalculateTangents();
        meniscusMesh.RecalculateBounds();
    }

    private void RebuildRimHighlightMesh()
    {
        if (rimHighlightMesh == null) return;

        int count = Mathf.Clamp(rimHighlightCount, 0, 64);
        if (count == 0)
        {
            rimHighlightMesh.Clear(false);
            return;
        }

        int quadVertices = count * 4;
        Vector3[] vertices = new Vector3[quadVertices];
        Vector3[] normals = new Vector3[quadVertices];
        Vector2[] uvs = new Vector2[quadVertices];
        int[] triangles = new int[count * 6];
        Vector3 normalLocal = GetSurfaceNormalLocal();
        float radius = Mathf.Max(0.02f, innerRadius - meniscusWidth * 0.55f);
        float beadSize = Mathf.Clamp(innerRadius * 0.022f, 0.004f, 0.018f);

        for (int i = 0; i < count; i++)
        {
            float angle01 = (i + 0.37f * Mathf.Sin(i * 12.9898f)) / count;
            float angle = angle01 * Mathf.PI * 2f;
            float jitter = (Mathf.PerlinNoise(i * 0.37f, Time.time * 0.11f) - 0.5f) * meniscusWidth;
            float r = radius + jitter;
            Vector3 center = new Vector3(liquidCenterLocalXZ.x + Mathf.Cos(angle) * r, 0f, liquidCenterLocalXZ.y + Mathf.Sin(angle) * r);
            center.y = GetSurfaceYAt(normalLocal, center.x, center.z, 1f, angle01) + meniscusHeight * 0.72f;

            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)).normalized * beadSize * RandomlessScale(i, 0.75f, 1.35f);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized * beadSize * 0.62f;
            int v = i * 4;
            vertices[v] = center - tangent - radial;
            vertices[v + 1] = center + tangent - radial;
            vertices[v + 2] = center - tangent + radial;
            vertices[v + 3] = center + tangent + radial;
            normals[v] = normals[v + 1] = normals[v + 2] = normals[v + 3] = normalLocal;
            uvs[v] = new Vector2(0f, 0f); uvs[v + 1] = new Vector2(1f, 0f); uvs[v + 2] = new Vector2(0f, 1f); uvs[v + 3] = new Vector2(1f, 1f);

            int t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        rimHighlightMesh.Clear(false);
        rimHighlightMesh.vertices = vertices;
        rimHighlightMesh.normals = normals;
        rimHighlightMesh.uv = uvs;
        rimHighlightMesh.triangles = triangles;
        rimHighlightMesh.RecalculateBounds();
    }

    private float RandomlessScale(int seed, float min, float max)
    {
        float n = Mathf.Sin(seed * 78.233f + 19.19f) * 43758.5453f;
        n = n - Mathf.Floor(n);
        return Mathf.Lerp(min, max, n);
    }

    private void CaptureBucketRenderers()
    {
        bucketRenderers.Clear();
        originalBucketColors.Clear();
        if (bucketRoot == null) return;

        Renderer[] renderers = bucketRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r.gameObject.name.StartsWith("InteriorPaint_")) continue;
            if (!IsBucketBodyRenderer(r)) continue;
            bucketRenderers.Add(r);
        }
    }

    private void ApplyBucketTransparencyIfNeeded(bool force)
    {
        if (bucketRoot == null) return;
        if (bucketRenderers.Count == 0) CaptureBucketRenderers();

        if (!presentationBucketTransparency)
        {
            if (bucketTransparencyApplied || force)
            {
                RestoreBucketMaterials();
            }
            bucketTransparencyApplied = false;
            return;
        }

        if (!force && bucketTransparencyApplied) return;
        bucketTransparencyApplied = true;

        for (int i = 0; i < bucketRenderers.Count; i++)
        {
            Renderer renderer = bucketRenderers[i];
            if (renderer == null) continue;
            Material[] materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                MakeMaterialTransparent(materials[m], bucketTransparencyAlpha);
            }
        }
    }

    private void RestoreBucketMaterials()
    {
        for (int i = 0; i < bucketRenderers.Count; i++)
        {
            Renderer renderer = bucketRenderers[i];
            if (renderer == null) continue;
            Material[] materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                RestoreMaterialOpaque(materials[m]);
            }
        }
    }

    private void RestoreMaterialOpaque(Material material)
    {
        if (material == null) return;

        if (originalBucketColors.TryGetValue(material, out Color original))
        {
            original.a = 1f;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", original);
            if (material.HasProperty("_Color")) material.SetColor("_Color", original);
        }

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        material.SetInt("_SrcBlend", (int)BlendMode.One);
        material.SetInt("_DstBlend", (int)BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Geometry;
    }

    private void MakeMaterialTransparent(Material material, float alpha)
    {
        if (material == null) return;

        if (!originalBucketColors.ContainsKey(material))
        {
            Color original = Color.white;
            if (material.HasProperty("_BaseColor")) original = material.GetColor("_BaseColor");
            else if (material.HasProperty("_Color")) original = material.GetColor("_Color");
            originalBucketColors[material] = original;
        }

        Color c = originalBucketColors[material];
        c.a = Mathf.Clamp01(alpha);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
        if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    public void ApplyQualityBudget(int segments, int rings, int sideSubdivisions, int rimHighlights, float rebuildInterval, bool volume, bool rimDetails)
    {
        radialSegments = Mathf.Clamp(segments, 32, 224);
        radialRings = Mathf.Clamp(rings, 8, 72);
        sideVerticalSubdivisions = Mathf.Clamp(sideSubdivisions, 2, 28);
        rimHighlightCount = Mathf.Clamp(rimHighlights, 0, 64);
        meshRebuildInterval = Mathf.Clamp(rebuildInterval, 0.02f, 0.18f);
        renderLiquidVolume = volume;
        renderRimHighlights = rimDetails;
        presentationBucketTransparency = false;
        RequestRebuild();
        ApplyBucketTransparencyIfNeeded(true);
    }

    public void ForceRebuild()
    {
        ValidateLiquidGeometry();
        UpdateFillFromEmitter();
        EnsureLiquidObjects();
        RebuildAllMeshes();
    }

    public void RequestRebuild()
    {
        forceRebuildRequested = true;
    }

    public void ResetLiquidVisual()
    {
        hasLastMotion = false;
        filteredAccelerationWorld = Vector3.zero;
        sloshVelocityWorld = Vector3.zero;
        sloshNormalWorld = Vector3.up;
        sloshOffsetLocal = Vector3.zero;
        sloshIntensity = 0f;
        ForceRebuild();
    }

    public void SetFill01(float value)
    {
        value = Mathf.Clamp01(value);
        fill01 = value;
        if (paintEmitter != null) paintEmitter.SetFill01(value);
        RequestRebuild();
    }

    public void RecalibrateNow()
    {
        calibratedOnce = false;
        TryAutoCalibrateGeometry(true);
        ForceRebuild();
    }

    public float Fill01 => fill01;
    public float SurfaceLocalY => surfaceLocalY;
    public float SloshIntensity => sloshIntensity;
    public float VisibleLiters => visibleLiters;
    public float EstimatedSurfaceArea => estimatedSurfaceArea;
    public string CalibrationStatus => calibrationStatus;
    public string StatusText => enableInteriorPaint ? (fill01 <= 0.002f ? "EMPTY" : "CONTAINED LIQUID") : "OFF";
}
