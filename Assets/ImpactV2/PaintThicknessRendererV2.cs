using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(PaintSurfaceStateV2))]
public class PaintThicknessRendererV2 : MonoBehaviour
{
    [Header("References")]
    public PaintSurfaceStateV2 surfaceState;
    public Transform surfaceTransform;

    [Header("Raised Paint Mesh")]
    public bool enableRaisedPaintMesh = false;
    [Range(16, 128)] public int meshResolution = 88;
    public float updateInterval = 0.055f;
    public float surfaceOffsetWorld = 0.0045f;
    public bool placeAboveMeshTopSurface = true;
    public float maxHeightWorld = 0.065f;
    public float thicknessSensitivity = 0.34f;
    [Range(0.45f, 2.2f)] public float heightGamma = 0.78f;
    [Range(0f, 1f)] public float smoothing = 0.68f;
    public float visibleThicknessCutoff = 0.010f;
    public float wetnessHeightBoost = 0.20f;

    [Header("Material")]
    public Material raisedPaintMaterial;
    public Color wetPaintColor = new Color(0.88f, 0.02f, 0.0f, 1f);
    public Color dryPaintColor = new Color(0.45f, 0.0f, 0.0f, 1f);
    [Range(0f, 1f)] public float smoothness = 0.88f;

    [Header("Runtime Stats")]
    public int visiblePatchCount;
    public float highestRenderedHeight;
    public int lastRenderedRevision = -1;

    private GameObject meshObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private float[] smoothedHeights;
    private readonly List<int> triangles = new List<int>(65536);

    private int lastMeshResolution;
    private float timer;

    void Awake()
    {
        AutoFindReferences();

        if (!enableRaisedPaintMesh)
        {
            HideMeshObject();
            return;
        }

        EnsureMeshObjects();
        ForceRefresh();
    }

    void OnEnable()
    {
        AutoFindReferences();

        if (!enableRaisedPaintMesh)
        {
            HideMeshObject();
            return;
        }

        EnsureMeshObjects();
        ForceRefresh();
    }

    void OnValidate()
    {
        meshResolution = Mathf.Clamp(meshResolution, 16, 128);
        updateInterval = Mathf.Max(0.01f, updateInterval);
        surfaceOffsetWorld = Mathf.Max(0f, surfaceOffsetWorld);
        maxHeightWorld = Mathf.Max(0.001f, maxHeightWorld);
        thicknessSensitivity = Mathf.Max(0.01f, thicknessSensitivity);
        visibleThicknessCutoff = Mathf.Max(0f, visibleThicknessCutoff);
        smoothing = Mathf.Clamp01(smoothing);
        smoothness = Mathf.Clamp01(smoothness);
    }

    private void HideMeshObject()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        if (meshObject != null)
        {
            meshObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (!enableRaisedPaintMesh)
        {
            HideMeshObject();
            return;
        }

        AutoFindReferences();
        EnsureMeshObjects();

        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
        }

        timer += Application.IsPlaying(gameObject) ? Time.deltaTime : 0.25f;

        bool revisionChanged = surfaceState != null && surfaceState.dataRevision != lastRenderedRevision;
        bool resolutionChanged = meshResolution != lastMeshResolution;

        if (revisionChanged || resolutionChanged || timer >= updateInterval)
        {
            timer = 0f;
            RefreshMesh(revisionChanged || resolutionChanged);
        }
    }

    private void AutoFindReferences()
    {
        if (surfaceState == null)
        {
            surfaceState = GetComponent<PaintSurfaceStateV2>();
        }

        if (surfaceTransform == null && surfaceState != null)
        {
            surfaceTransform = surfaceState.surfaceTransform != null ? surfaceState.surfaceTransform : surfaceState.transform;
        }
    }

    private void EnsureMeshObjects()
    {
        if (surfaceTransform == null)
        {
            return;
        }

        if (meshObject == null)
        {
            Transform existing = transform.Find("RaisedPaintThicknessMesh_Runtime");

            if (existing != null)
            {
                meshObject = existing.gameObject;
            }
            else
            {
                meshObject = new GameObject("RaisedPaintThicknessMesh_Runtime");
                meshObject.transform.SetParent(surfaceTransform, false);
            }
        }

        meshObject.hideFlags = Application.IsPlaying(gameObject)
            ? HideFlags.DontSave
            : HideFlags.HideAndDontSave;

        meshObject.transform.localPosition = Vector3.zero;
        meshObject.transform.localRotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;

        if (meshFilter == null)
        {
            meshFilter = meshObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = meshObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = meshObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = meshObject.AddComponent<MeshRenderer>();
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Runtime_Raised_Paint_Thickness_Mesh";
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }

        if (meshFilter.sharedMesh != mesh)
        {
            meshFilter.sharedMesh = mesh;
        }

        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (raisedPaintMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            raisedPaintMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"));
            raisedPaintMaterial.name = "MAT_Runtime_Raised_Wet_Paint";
        }

        if (raisedPaintMaterial.HasProperty("_BaseColor"))
        {
            raisedPaintMaterial.SetColor("_BaseColor", wetPaintColor);
        }

        if (raisedPaintMaterial.HasProperty("_Color"))
        {
            raisedPaintMaterial.SetColor("_Color", wetPaintColor);
        }

        if (raisedPaintMaterial.HasProperty("_Smoothness"))
        {
            raisedPaintMaterial.SetFloat("_Smoothness", smoothness);
        }

        if (raisedPaintMaterial.HasProperty("_Metallic"))
        {
            raisedPaintMaterial.SetFloat("_Metallic", 0f);
        }

        meshRenderer.sharedMaterial = raisedPaintMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
    }

    public void ForceRefresh()
    {
        lastRenderedRevision = -1;
        RefreshMesh(true);
    }

    private void RefreshMesh(bool forceFullRebuild)
    {
        if (surfaceState == null || !surfaceState.HasValidMaps || mesh == null)
        {
            return;
        }

        int res = Mathf.Clamp(meshResolution, 16, 128);
        int vertexCount = res * res;

        if (vertices == null || vertices.Length != vertexCount || forceFullRebuild || res != lastMeshResolution)
        {
            vertices = new Vector3[vertexCount];
            uvs = new Vector2[vertexCount];
            smoothedHeights = new float[vertexCount];
            lastMeshResolution = res;
        }

        highestRenderedHeight = 0f;

        float inverseScaleY = 1f / Mathf.Max(0.0001f, Mathf.Abs(surfaceTransform.lossyScale.y));
        float baseLocalSurfaceY = GetVisualSurfaceLocalY();
        float localOffset = surfaceOffsetWorld * inverseScaleY;
        float localMaxHeight = maxHeightWorld * inverseScaleY;

        for (int y = 0; y < res; y++)
        {
            float v = y / (float)(res - 1);

            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1);
                int index = y * res + x;

                float rawThickness = surfaceState.SampleThicknessBilinear(u, v);
                float wetness = surfaceState.SampleWetnessBilinear(u, v);
                float height01 = 1f - Mathf.Exp(-Mathf.Max(0f, rawThickness) * thicknessSensitivity);
                height01 = Mathf.Pow(height01, heightGamma);
                height01 *= Mathf.Lerp(1f, 1f + wetnessHeightBoost, Mathf.Clamp01(wetness));
                height01 = Mathf.Clamp01(height01);

                float targetHeight = height01 * localMaxHeight;
                smoothedHeights[index] = Mathf.Lerp(smoothedHeights[index], targetHeight, 1f - smoothing);

                highestRenderedHeight = Mathf.Max(highestRenderedHeight, smoothedHeights[index] / inverseScaleY);

                vertices[index] = new Vector3(u - 0.5f, baseLocalSurfaceY + localOffset + smoothedHeights[index], v - 0.5f);
                uvs[index] = new Vector2(u, v);
            }
        }

        BuildVisibleTriangles(res);

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        lastRenderedRevision = surfaceState.dataRevision;
    }

    private float GetVisualSurfaceLocalY()
    {
        if (!placeAboveMeshTopSurface || surfaceTransform == null)
        {
            return 0f;
        }

        MeshFilter filter = surfaceTransform.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null)
        {
            return 0f;
        }

        return filter.sharedMesh.bounds.max.y;
    }

    private void BuildVisibleTriangles(int res)
    {
        triangles.Clear();
        visiblePatchCount = 0;

        float localCutoff = visibleThicknessCutoff / Mathf.Max(0.0001f, Mathf.Abs(surfaceTransform.lossyScale.y));

        for (int y = 0; y < res - 1; y++)
        {
            for (int x = 0; x < res - 1; x++)
            {
                int a = y * res + x;
                int b = y * res + x + 1;
                int c = (y + 1) * res + x;
                int d = (y + 1) * res + x + 1;

                float highest = Mathf.Max(
                    Mathf.Max(smoothedHeights[a], smoothedHeights[b]),
                    Mathf.Max(smoothedHeights[c], smoothedHeights[d])
                );

                if (highest <= localCutoff)
                {
                    continue;
                }

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);

                visiblePatchCount++;
            }
        }
    }
}
