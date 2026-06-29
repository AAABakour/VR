using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RopeRigController : MonoBehaviour
{
    [Header("Endpoints")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Visual Mode")]
    public RopeRigMode mode = RopeRigMode.Chain;
    [Min(2)]
    public int segmentCount = 18;
    [Min(0.001f)]
    public float ropeThickness = 0.035f;
    [Min(0f)]
    public float sagAmount = 0.18f;
    [Range(0f, 1f)]
    public float smoothing = 0.35f;

    [Header("Prefabs")]
    public GameObject chainLinkPrefab;
    public GameObject ropeSegmentPrefab;
    public bool generatePrimitiveFallback = true;

    [Header("Runtime")]
    public bool updateInLateUpdate = true;
    public bool showDebugGizmos = true;

    private readonly List<RopeRigSegment> segments = new List<RopeRigSegment>();
    private int builtSegmentCount = -1;
    private RopeRigMode builtMode;
    private GameObject builtChainPrefab;
    private GameObject builtRopePrefab;

    private void Awake()
    {
        EnsureSegments();
        UpdateSegments(1f);
    }

    private void OnEnable()
    {
        EnsureSegments();
        UpdateSegments(1f);
    }

    private void OnValidate()
    {
        segmentCount = Mathf.Max(2, segmentCount);
        ropeThickness = Mathf.Max(0.001f, ropeThickness);
        EnsureSegments();
        UpdateSegments(1f);
    }

    private void Update()
    {
        if (!updateInLateUpdate)
        {
            UpdateSegments(Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (updateInLateUpdate)
        {
            UpdateSegments(Time.deltaTime);
        }
    }

    [ContextMenu("Regenerate Segments")]
    public void RegenerateSegments()
    {
        ClearSegments();
        EnsureSegments();
        UpdateSegments(1f);
    }

    public Vector3 EvaluatePoint(float normalizedPosition)
    {
        if (startPoint == null || endPoint == null)
        {
            return transform.position;
        }

        float t = Mathf.Clamp01(normalizedPosition);
        Vector3 start = startPoint.position;
        Vector3 end = endPoint.position;
        Vector3 point = Vector3.Lerp(start, end, t);

        float sag = Mathf.Sin(t * Mathf.PI) * sagAmount;
        point += Vector3.down * sag;

        return point;
    }

    private void EnsureSegments()
    {
        CollectExistingSegments();

        bool needsRebuild =
            builtSegmentCount != segmentCount ||
            builtMode != mode ||
            builtChainPrefab != chainLinkPrefab ||
            builtRopePrefab != ropeSegmentPrefab ||
            segments.Count != segmentCount;

        if (!needsRebuild)
        {
            return;
        }

        ClearSegments();

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segmentObject = CreateSegmentObject(i);

            if (segmentObject == null)
            {
                continue;
            }

            segmentObject.transform.SetParent(transform, false);
            segmentObject.name = mode == RopeRigMode.Chain
                ? "ChainLink_" + i.ToString("00")
                : "RopeSegment_" + i.ToString("00");

            RopeRigSegment segment = segmentObject.GetComponent<RopeRigSegment>();

            if (segment == null)
            {
                segment = segmentObject.AddComponent<RopeRigSegment>();
            }

            segment.segmentIndex = i;
            segment.CacheRenderer();
            segments.Add(segment);
        }

        builtSegmentCount = segmentCount;
        builtMode = mode;
        builtChainPrefab = chainLinkPrefab;
        builtRopePrefab = ropeSegmentPrefab;
    }

    private GameObject CreateSegmentObject(int index)
    {
        GameObject prefab = mode == RopeRigMode.Chain ? chainLinkPrefab : ropeSegmentPrefab;

        if (prefab != null)
        {
            return Instantiate(prefab);
        }

        if (!generatePrimitiveFallback)
        {
            return null;
        }

        PrimitiveType primitiveType = mode == RopeRigMode.Chain
            ? PrimitiveType.Cube
            : PrimitiveType.Cylinder;

        GameObject segmentObject = GameObject.CreatePrimitive(primitiveType);
        Collider segmentCollider = segmentObject.GetComponent<Collider>();

        if (segmentCollider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(segmentCollider);
            }
            else
            {
                DestroyImmediate(segmentCollider);
            }
        }

        return segmentObject;
    }

    private void CollectExistingSegments()
    {
        segments.Clear();
        GetComponentsInChildren(true, segments);
        segments.Sort((a, b) => a.segmentIndex.CompareTo(b.segmentIndex));
    }

    private void ClearSegments()
    {
        CollectExistingSegments();

        for (int i = segments.Count - 1; i >= 0; i--)
        {
            if (segments[i] == null)
            {
                continue;
            }

            GameObject segmentObject = segments[i].gameObject;

            if (Application.isPlaying)
            {
                Destroy(segmentObject);
            }
            else
            {
                DestroyImmediate(segmentObject);
            }
        }

        segments.Clear();
        builtSegmentCount = -1;
    }

    private void UpdateSegments(float deltaTime)
    {
        if (startPoint == null || endPoint == null)
        {
            return;
        }

        EnsureSegments();

        float blend = Application.isPlaying
            ? 1f - Mathf.Exp(-Mathf.Max(0.01f, 1f - smoothing) * 30f * Mathf.Max(deltaTime, 0.001f))
            : 1f;

        for (int i = 0; i < segments.Count; i++)
        {
            RopeRigSegment segment = segments[i];

            if (segment == null)
            {
                continue;
            }

            float t0 = i / (float)segmentCount;
            float t1 = (i + 1) / (float)segmentCount;

            Vector3 p0 = EvaluatePoint(t0);
            Vector3 p1 = EvaluatePoint(t1);
            Vector3 direction = p1 - p0;

            if (direction.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            Transform segmentTransform = segment.transform;
            Vector3 targetPosition = (p0 + p1) * 0.5f;
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            float targetLength = direction.magnitude;

            if (mode == RopeRigMode.Chain && (i & 1) == 1)
            {
                targetRotation *= Quaternion.AngleAxis(90f, Vector3.up);
            }

            segmentTransform.position = Vector3.Lerp(segmentTransform.position, targetPosition, blend);
            segmentTransform.rotation = Quaternion.Slerp(segmentTransform.rotation, targetRotation, blend);

            if (mode == RopeRigMode.Rope)
            {
                segmentTransform.localScale = new Vector3(
                    ropeThickness,
                    targetLength * 0.5f,
                    ropeThickness
                );
            }
            else
            {
                segmentTransform.localScale = new Vector3(
                    ropeThickness * 2.4f,
                    Mathf.Max(targetLength * 0.8f, ropeThickness * 2f),
                    ropeThickness * 0.9f
                );
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || startPoint == null || endPoint == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(startPoint.position, 0.06f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(endPoint.position, 0.06f);

        Gizmos.color = mode == RopeRigMode.Chain ? Color.green : Color.white;

        Vector3 previous = EvaluatePoint(0f);

        for (int i = 1; i <= 32; i++)
        {
            float t = i / 32f;
            Vector3 current = EvaluatePoint(t);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}
