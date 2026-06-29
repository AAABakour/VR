using UnityEngine;

[DefaultExecutionOrder(35)]
public class FluidBoxController : MonoBehaviour
{
    public Vector3 boundsSize = new Vector3(4f, 2f, 2f);
    public Transform boxVisual;
    public Transform boundsGizmoRoot;
    public Material glassMaterialTemplate;
    public Material edgeMaterialTemplate;
    public Color glassColor = new Color(0.35f, 0.75f, 1f, 0.18f);
    public Color edgeColor = new Color(0.7f, 0.95f, 1f, 0.9f);
    public bool generateRuntimeVisuals = true;

    private Material glassMaterial;
    private Material edgeMaterial;
    private LineRenderer[] edgeLines;
    private Vector3 previousPosition;
    private Vector3 velocity;
    private Vector3 acceleration;
    private bool hasPreviousSample;

    public Matrix4x4 LocalToWorldMatrix
    {
        get { return transform.localToWorldMatrix; }
    }

    public Matrix4x4 WorldToLocalMatrix
    {
        get { return transform.worldToLocalMatrix; }
    }

    public Vector3 LocalGravity
    {
        get { return transform.InverseTransformDirection(Physics.gravity - acceleration); }
    }

    public Vector3 BoundsSize
    {
        get { return boundsSize; }
    }

    public Vector3 Velocity
    {
        get { return velocity; }
    }

    private void Awake()
    {
        previousPosition = transform.position;
        hasPreviousSample = true;
        EnsureVisuals();
    }

    private void OnValidate()
    {
        boundsSize = new Vector3(
            Mathf.Max(0.1f, boundsSize.x),
            Mathf.Max(0.1f, boundsSize.y),
            Mathf.Max(0.1f, boundsSize.z)
        );
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        if (!hasPreviousSample)
        {
            previousPosition = transform.position;
            hasPreviousSample = true;
            return;
        }

        Vector3 newVelocity = (transform.position - previousPosition) / dt;
        acceleration = (newVelocity - velocity) / dt;
        velocity = newVelocity;
        previousPosition = transform.position;

        EnsureVisuals();
        UpdateVisuals();
    }

    public void ResetMotionSample()
    {
        previousPosition = transform.position;
        velocity = Vector3.zero;
        acceleration = Vector3.zero;
        hasPreviousSample = true;
    }

    private void EnsureVisuals()
    {
        if (!generateRuntimeVisuals)
        {
            return;
        }

        if (boxVisual == null)
        {
            Transform found = transform.Find("BoxVisual");
            if (found != null)
            {
                boxVisual = found;
            }
        }

        if (boxVisual == null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BoxVisual";
            cube.transform.SetParent(transform, false);
            boxVisual = cube.transform;
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        MeshRenderer renderer = boxVisual.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = boxVisual.gameObject.AddComponent<MeshRenderer>();
        }

        MeshFilter filter = boxVisual.GetComponent<MeshFilter>();
        if (filter == null)
        {
            GameObject temporaryCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temporaryCube.SetActive(false);
            MeshFilter temporaryFilter = temporaryCube.GetComponent<MeshFilter>();
            filter = boxVisual.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = temporaryFilter.sharedMesh;
            Destroy(temporaryCube);
        }

        if (glassMaterial == null)
        {
            if (glassMaterialTemplate != null)
            {
                glassMaterial = glassMaterialTemplate;
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    Debug.LogWarning("[FluidBoxController] No fallback transparent shader was found.", this);
                    return;
                }

                glassMaterial = new Material(shader);
                glassMaterial.name = "Runtime Transparent Fluid Box Glass";
                glassMaterial.color = glassColor;
                if (glassMaterial.HasProperty("_BaseColor"))
                {
                    glassMaterial.SetColor("_BaseColor", glassColor);
                }
                if (glassMaterial.HasProperty("_Color"))
                {
                    glassMaterial.SetColor("_Color", glassColor);
                }
                glassMaterial.SetFloat("_Surface", 1f);
                glassMaterial.SetFloat("_Blend", 0f);
                glassMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glassMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glassMaterial.SetFloat("_ZWrite", 0f);
                glassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glassMaterial.renderQueue = 3000;
            }
        }

        renderer.sharedMaterial = glassMaterial;

        if (boundsGizmoRoot == null)
        {
            Transform found = transform.Find("BoxBoundsGizmos");
            if (found != null)
            {
                boundsGizmoRoot = found;
            }
        }

        if (boundsGizmoRoot == null)
        {
            GameObject edgeRoot = new GameObject("BoxBoundsGizmos");
            edgeRoot.transform.SetParent(transform, false);
            boundsGizmoRoot = edgeRoot.transform;
        }

        if (edgeLines == null || edgeLines.Length != 12)
        {
            CreateEdgeLines();
        }
    }

    private void CreateEdgeLines()
    {
        edgeLines = new LineRenderer[12];
        if (edgeMaterial == null)
        {
            if (edgeMaterialTemplate != null)
            {
                edgeMaterial = edgeMaterialTemplate;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                edgeMaterial = new Material(shader);
                edgeMaterial.name = "Runtime Transparent Fluid Box Edges";
                edgeMaterial.color = edgeColor;
            }
        }

        for (int i = 0; i < edgeLines.Length; i++)
        {
            GameObject lineObject = new GameObject("BoxEdge_" + i.ToString("00"));
            lineObject.transform.SetParent(boundsGizmoRoot, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = 0.015f;
            line.endWidth = 0.015f;
            line.sharedMaterial = edgeMaterial;
            edgeLines[i] = line;
        }
    }

    private void UpdateVisuals()
    {
        if (boxVisual != null)
        {
            boxVisual.localPosition = Vector3.zero;
            boxVisual.localRotation = Quaternion.identity;
            boxVisual.localScale = boundsSize;
        }

        if (edgeLines == null || edgeLines.Length != 12)
        {
            return;
        }

        Vector3 h = boundsSize * 0.5f;
        Vector3[] c =
        {
            new Vector3(-h.x, -h.y, -h.z),
            new Vector3( h.x, -h.y, -h.z),
            new Vector3( h.x, -h.y,  h.z),
            new Vector3(-h.x, -h.y,  h.z),
            new Vector3(-h.x,  h.y, -h.z),
            new Vector3( h.x,  h.y, -h.z),
            new Vector3( h.x,  h.y,  h.z),
            new Vector3(-h.x,  h.y,  h.z)
        };

        SetEdge(0, c[0], c[1]);
        SetEdge(1, c[1], c[2]);
        SetEdge(2, c[2], c[3]);
        SetEdge(3, c[3], c[0]);
        SetEdge(4, c[4], c[5]);
        SetEdge(5, c[5], c[6]);
        SetEdge(6, c[6], c[7]);
        SetEdge(7, c[7], c[4]);
        SetEdge(8, c[0], c[4]);
        SetEdge(9, c[1], c[5]);
        SetEdge(10, c[2], c[6]);
        SetEdge(11, c[3], c[7]);
    }

    private void SetEdge(int index, Vector3 a, Vector3 b)
    {
        edgeLines[index].SetPosition(0, a);
        edgeLines[index].SetPosition(1, b);
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = edgeColor;
        Gizmos.DrawWireCube(Vector3.zero, boundsSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
