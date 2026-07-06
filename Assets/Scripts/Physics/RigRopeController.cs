using UnityEngine;

[ExecuteAlways]
public class RigRopeController : MonoBehaviour
{
    public enum RopeType
    {
        Cotton,
        Nylon,
        Steel
    }

    public enum RopeVisualMode
    {
        TensionedCable,
        PhysicalCable,
        StraightReference
    }

    [Header("References")]
    public Transform pivotPoint;
    public Transform bucket;
    public Transform bucketAnchor;
    public PendulumController pendulumController;

    [Header("Rope Type")]
    public RopeType ropeType = RopeType.Nylon;
    public bool applyTypePhysicsPreset = true;

    [Header("Visual Rope Mesh")]
    public GameObject ropeSegmentPrefab;

    [Header("Rope Materials")]
    public Material cottonMaterial;
    public Material nylonMaterial;
    public Material steelMaterial;

    [Header("Rope Shape")]
    public int segmentCount = 28;
    public float ropeLength = 2.2f;
    public float chainRadius = 0.018f;
    public bool showDebugNodes = false;
    public RopeVisualMode visualMode = RopeVisualMode.TensionedCable;

    [Header("Advanced Tension Solver")]
    [Tooltip("Higher values keep the cable visually tight. Use 0.92-0.99 for a loaded bucket.")]
    [Range(0f, 1f)] public float tautness = 0.97f;
    [Tooltip("Pulls internal rope nodes toward the physically plausible tension curve.")]
    [Range(0f, 1f)] public float shapeFollow = 0.92f;
    [Tooltip("Keeps the rope from folding into a soft cloth shape.")]
    [Range(0f, 1f)] public float bendStiffness = 0.96f;
    [Tooltip("Maximum visible sag when the rope becomes genuinely slack.")]
    public float maxSag = 0.055f;
    [Tooltip("How much shorter the pivot-anchor distance must be before the rope is allowed to sag.")]
    [Range(0.01f, 0.35f)] public float slackDetectionRange = 0.16f;
    [Tooltip("Small dynamic vibration caused by bucket motion. Keep low for realism.")]
    public float dynamicWaveAmplitude = 0.015f;
    public float dynamicWaveFrequency = 4.5f;
    [Tooltip("Limits how much the visible rope can stretch beyond its rest length.")]
    public float maxVisualStretch = 0.045f;
    [Tooltip("Simulation substeps used before drawing the rope.")]
    public int solverSubsteps = 2;

    [Header("Physics Settings")]
    public float gravity = 0.45f;
    public float damping = 0.992f;
    public int constraintIterations = 80;

    [Header("Tension Visualization")]
    public bool enableTensionColor = false;
    public float mediumTensionThreshold = 0.35f;
    public float highTensionThreshold = 0.75f;

    public Color relaxedColor = new Color(0.45f, 0.35f, 0.22f);
    public Color mediumTensionColor = new Color(1f, 0.75f, 0.1f);
    public Color highTensionColor = new Color(1f, 0.1f, 0.05f);

    [Header("Rope Results")]
    public float elasticity = 0.025f;
    public float tension;
    public float slack01;
    public float endpointDistance;
    public float effectiveRopeLength;

    private Vector3[] currentPositions;
    private Vector3[] previousPositions;
    private Vector3[] targetPositions;

    private GameObject[] nodeObjects;
    private GameObject[] chainSegments;
    private Renderer[] chainRenderers;

    private float segmentLength;
    private float lastRopeLength;
    private int lastSegmentCount;
    private float lastChainRadius;
    private RopeType lastAppliedRopeType;
    private RopeType lastPresetRopeType;
    private Material fallbackMaterial;
    private bool forceRebuild;
    private Vector3 lastAttachPosition;
    private Vector3 smoothedBucketVelocity;
    private float simulatedTime;

    void OnEnable()
    {
        forceRebuild = true;
        RebuildIfPossible();
    }

    void Start()
    {
        forceRebuild = true;
        RebuildIfPossible();
    }

    void OnValidate()
    {
        segmentCount = Mathf.Clamp(segmentCount, 4, 96);
        ropeLength = Mathf.Max(0.05f, ropeLength);
        chainRadius = Mathf.Max(0.001f, chainRadius);
        constraintIterations = Mathf.Clamp(constraintIterations, 4, 160);
        solverSubsteps = Mathf.Clamp(solverSubsteps, 1, 8);
        gravity = Mathf.Max(0f, gravity);
        damping = Mathf.Clamp(damping, 0.80f, 0.9999f);
        maxSag = Mathf.Max(0f, maxSag);
        dynamicWaveAmplitude = Mathf.Max(0f, dynamicWaveAmplitude);
        maxVisualStretch = Mathf.Max(0f, maxVisualStretch);
        slackDetectionRange = Mathf.Clamp(slackDetectionRange, 0.01f, 0.35f);
        forceRebuild = true;
    }

    void Update()
    {
        if (!Application.IsPlaying(gameObject))
        {
            TickRope(0f, false);
        }
    }

    void LateUpdate()
    {
        if (Application.IsPlaying(gameObject))
        {
            TickRope(Time.deltaTime, true);
        }
    }

    void TickRope(float dt, bool simulate)
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        dt = Mathf.Clamp(dt, 0f, 0.033f);

        if (pendulumController != null)
        {
            ropeLength = Mathf.Max(0.05f, pendulumController.ropeLength);
        }

        ApplyRopeType();

        if (forceRebuild || !ArraysAreValid() || segmentCount != lastSegmentCount)
        {
            RebuildIfPossible();
        }

        if (!ArraysAreValid())
        {
            return;
        }

        if (ropeType != lastAppliedRopeType)
        {
            ApplyRopeMaterialByType();
        }

        if (Mathf.Abs(lastRopeLength - ropeLength) > 0.001f)
        {
            ResetRopeShape();
            lastRopeLength = ropeLength;
        }

        if (Mathf.Abs(lastChainRadius - chainRadius) > 0.001f)
        {
            lastChainRadius = chainRadius;
        }

        UpdateEndpointMotion(dt);
        BuildTargetCurve();

        if (!simulate || visualMode == RopeVisualMode.StraightReference)
        {
            SnapToTargetCurve();
        }
        else
        {
            SimulateRope(dt);
        }

        CalculateTension();

        if (enableTensionColor)
        {
            ApplyTensionColor();
        }
        else
        {
            ApplyRopeMaterialByType();
        }

        DrawNodeObjects();
        DrawChainSegments();
    }

    bool HasRequiredReferences()
    {
        if (pivotPoint == null)
        {
            return false;
        }

        if (bucket == null && bucketAnchor == null)
        {
            return false;
        }

        return true;
    }

    bool ArraysAreValid()
    {
        if (currentPositions == null || previousPositions == null || targetPositions == null)
        {
            return false;
        }

        if (nodeObjects == null || chainSegments == null || chainRenderers == null)
        {
            return false;
        }

        if (currentPositions.Length != segmentCount || previousPositions.Length != segmentCount || targetPositions.Length != segmentCount)
        {
            return false;
        }

        if (nodeObjects.Length != segmentCount)
        {
            return false;
        }

        if (chainSegments.Length != segmentCount - 1 || chainRenderers.Length != segmentCount - 1)
        {
            return false;
        }

        return true;
    }

    void RebuildIfPossible()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        ClearGeneratedObjects();
        ApplyRopeType();

        currentPositions = new Vector3[segmentCount];
        previousPositions = new Vector3[segmentCount];
        targetPositions = new Vector3[segmentCount];

        nodeObjects = new GameObject[segmentCount];
        chainSegments = new GameObject[segmentCount - 1];
        chainRenderers = new Renderer[segmentCount - 1];

        InitializeRope();
        CreateNodeObjects();
        CreateChainSegments();
        ApplyRopeMaterialByType();
        DrawNodeObjects();
        DrawChainSegments();

        lastRopeLength = ropeLength;
        lastSegmentCount = segmentCount;
        lastChainRadius = chainRadius;
        lastAppliedRopeType = ropeType;
        lastPresetRopeType = ropeType;
        forceRebuild = false;

        DisableUnusedLineRenderer();
    }

    void DisableUnusedLineRenderer()
    {
        LineRenderer lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    void ClearGeneratedObjects()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (child.name.StartsWith("Rope_Node_") || child.name.StartsWith("Rope_Chain_Segment_"))
            {
                SafeDestroy(child.gameObject);
            }
        }
    }

    void SafeDestroy(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.IsPlaying(gameObject))
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    Vector3 GetBucketAttachPosition()
    {
        if (bucketAnchor != null)
        {
            return bucketAnchor.position;
        }

        if (bucket != null)
        {
            return bucket.position;
        }

        return pivotPoint.position + Vector3.down * ropeLength;
    }

    void ApplyRopeType()
    {
        if (!applyTypePhysicsPreset)
        {
            return;
        }

        if (ropeType == lastPresetRopeType && !forceRebuild)
        {
            return;
        }

        if (ropeType == RopeType.Cotton)
        {
            elasticity = 0.040f;
            damping = 0.989f;
            gravity = 0.55f;
            tautness = 0.94f;
            shapeFollow = 0.90f;
            bendStiffness = 0.94f;
            maxSag = 0.075f;
            dynamicWaveAmplitude = 0.022f;
            maxVisualStretch = 0.060f;
        }
        else if (ropeType == RopeType.Nylon)
        {
            elasticity = 0.025f;
            damping = 0.993f;
            gravity = 0.42f;
            tautness = 0.970f;
            shapeFollow = 0.94f;
            bendStiffness = 0.965f;
            maxSag = 0.050f;
            dynamicWaveAmplitude = 0.014f;
            maxVisualStretch = 0.040f;
        }
        else if (ropeType == RopeType.Steel)
        {
            elasticity = 0.006f;
            damping = 0.997f;
            gravity = 0.18f;
            tautness = 0.992f;
            shapeFollow = 0.985f;
            bendStiffness = 0.992f;
            maxSag = 0.018f;
            dynamicWaveAmplitude = 0.004f;
            maxVisualStretch = 0.012f;
        }

        lastPresetRopeType = ropeType;
    }

    Material GetMaterialForCurrentType()
    {
        if (ropeType == RopeType.Cotton && cottonMaterial != null)
        {
            return cottonMaterial;
        }

        if (ropeType == RopeType.Nylon && nylonMaterial != null)
        {
            return nylonMaterial;
        }

        if (ropeType == RopeType.Steel && steelMaterial != null)
        {
            return steelMaterial;
        }

        if (fallbackMaterial == null)
        {
            fallbackMaterial = CreateRopeMaterial(relaxedColor);
        }

        return fallbackMaterial;
    }

    void ApplyRopeMaterialByType()
    {
        if (chainRenderers == null)
        {
            return;
        }

        Material selectedMaterial = GetMaterialForCurrentType();

        for (int i = 0; i < chainRenderers.Length; i++)
        {
            if (chainRenderers[i] != null)
            {
                chainRenderers[i].material = selectedMaterial;
            }
        }

        lastAppliedRopeType = ropeType;
    }

    void InitializeRope()
    {
        UpdateEndpointMotion(0f);
        BuildTargetCurve();

        for (int i = 0; i < segmentCount; i++)
        {
            currentPositions[i] = targetPositions[i];
            previousPositions[i] = targetPositions[i];
        }
    }

    void ResetRopeShape()
    {
        UpdateEndpointMotion(0f);
        BuildTargetCurve();
        SnapToTargetCurve();
    }

    void UpdateEndpointMotion(float dt)
    {
        Vector3 attachPosition = GetBucketAttachPosition();

        if (dt > 0f)
        {
            Vector3 rawVelocity = (attachPosition - lastAttachPosition) / dt;
            float velocityBlend = 1f - Mathf.Exp(-18f * dt);
            smoothedBucketVelocity = Vector3.Lerp(smoothedBucketVelocity, rawVelocity, velocityBlend);
        }
        else
        {
            smoothedBucketVelocity = Vector3.zero;
        }

        lastAttachPosition = attachPosition;
        simulatedTime += dt;
    }

    void BuildTargetCurve()
    {
        Vector3 start = pivotPoint.position;
        Vector3 end = GetBucketAttachPosition();
        Vector3 axis = end - start;
        endpointDistance = axis.magnitude;

        if (endpointDistance < 0.001f)
        {
            axis = Vector3.down;
            endpointDistance = 0.001f;
        }

        Vector3 direction = axis / endpointDistance;
        float slackLength = Mathf.Max(0f, ropeLength - endpointDistance);
        slack01 = Mathf.Clamp01(slackLength / Mathf.Max(ropeLength * slackDetectionRange, 0.001f));

        float stretchAllowance = maxVisualStretch + elasticity * 0.15f;
        effectiveRopeLength = Mathf.Clamp(
            Mathf.Max(endpointDistance, ropeLength - slackLength * tautness),
            endpointDistance,
            ropeLength + stretchAllowance
        );

        if (visualMode == RopeVisualMode.TensionedCable)
        {
            effectiveRopeLength = Mathf.Lerp(endpointDistance, effectiveRopeLength, slack01);
        }
        else if (visualMode == RopeVisualMode.StraightReference)
        {
            effectiveRopeLength = endpointDistance;
            slack01 = 0f;
        }

        segmentLength = Mathf.Max(effectiveRopeLength / Mathf.Max(segmentCount - 1, 1), 0.001f);

        Vector3 side = Vector3.Cross(Vector3.up, direction);
        if (side.sqrMagnitude < 0.0001f)
        {
            side = Vector3.Cross(Vector3.forward, direction);
        }
        side.Normalize();

        Vector3 lateralWaveDirection = Vector3.ProjectOnPlane(smoothedBucketVelocity, direction);
        if (lateralWaveDirection.sqrMagnitude < 0.0001f)
        {
            lateralWaveDirection = side;
        }
        else
        {
            lateralWaveDirection.Normalize();
        }

        float speed01 = Mathf.Clamp01(smoothedBucketVelocity.magnitude / 4.0f);
        float dynamicScale = visualMode == RopeVisualMode.TensionedCable ? 1f - slack01 * 0.45f : 1f;
        float sagAmount = maxSag * slack01;
        float waveAmount = dynamicWaveAmplitude * speed01 * dynamicScale;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = (float)i / (segmentCount - 1);
            Vector3 linePoint = Vector3.Lerp(start, end, t);
            float curveMask = Mathf.Sin(t * Mathf.PI);

            Vector3 sagOffset = Vector3.down * sagAmount * curveMask;
            Vector3 waveOffset = Vector3.zero;

            if (Application.IsPlaying(gameObject) && waveAmount > 0f)
            {
                float wave = Mathf.Sin(simulatedTime * dynamicWaveFrequency + t * Mathf.PI * 2.0f);
                waveOffset = lateralWaveDirection * wave * waveAmount * curveMask;
            }

            targetPositions[i] = linePoint + sagOffset + waveOffset;
        }

        targetPositions[0] = start;
        targetPositions[segmentCount - 1] = end;
    }

    void CreateNodeObjects()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            node.name = "Rope_Node_" + i;
            node.transform.SetParent(transform);
            node.transform.localScale = Vector3.one * 0.025f;

            Collider nodeCollider = node.GetComponent<Collider>();

            if (nodeCollider != null)
            {
                SafeDestroy(nodeCollider);
            }

            node.SetActive(showDebugNodes);
            nodeObjects[i] = node;
        }
    }

    void CreateChainSegments()
    {
        for (int i = 0; i < segmentCount - 1; i++)
        {
            GameObject segment;

            if (ropeSegmentPrefab != null)
            {
                segment = Instantiate(ropeSegmentPrefab, transform);
            }
            else
            {
                segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                segment.transform.SetParent(transform);
            }

            segment.SetActive(true);
            segment.name = "Rope_Chain_Segment_" + i;

            RemoveNestedRopeControllers(segment);
            RemoveColliders(segment);

            Renderer segmentRenderer = GetOrCreateRenderer(segment);

            if (segmentRenderer != null)
            {
                segmentRenderer.enabled = true;
                segmentRenderer.material = GetMaterialForCurrentType();
            }

            chainSegments[i] = segment;
            chainRenderers[i] = segmentRenderer;
        }
    }

    void RemoveColliders(GameObject segment)
    {
        Collider[] colliders = segment.GetComponentsInChildren<Collider>(true);

        for (int c = 0; c < colliders.Length; c++)
        {
            if (colliders[c] != null)
            {
                SafeDestroy(colliders[c]);
            }
        }
    }

    void RemoveNestedRopeControllers(GameObject segment)
    {
        RigRopeController[] controllers = segment.GetComponentsInChildren<RigRopeController>(true);

        for (int c = 0; c < controllers.Length; c++)
        {
            if (controllers[c] != null && controllers[c] != this)
            {
                SafeDestroy(controllers[c]);
            }
        }
    }

    Renderer GetOrCreateRenderer(GameObject segment)
    {
        Renderer segmentRenderer = segment.GetComponentInChildren<Renderer>(true);

        if (segmentRenderer != null)
        {
            return segmentRenderer;
        }

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Generated_Visual_Mesh";
        visual.transform.SetParent(segment.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Collider visualCollider = visual.GetComponent<Collider>();

        if (visualCollider != null)
        {
            SafeDestroy(visualCollider);
        }

        return visual.GetComponent<Renderer>();
    }

    Material CreateRopeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        SetMaterialColor(material, color);

        return material;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    void SnapToTargetCurve()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            currentPositions[i] = targetPositions[i];
            previousPositions[i] = targetPositions[i];
        }
    }

    void SimulateRope(float dt)
    {
        int substeps = Mathf.Max(1, solverSubsteps);
        float subDt = dt / substeps;

        for (int step = 0; step < substeps; step++)
        {
            currentPositions[0] = targetPositions[0];
            currentPositions[segmentCount - 1] = targetPositions[segmentCount - 1];

            float gravityScale = visualMode == RopeVisualMode.TensionedCable
                ? Mathf.Lerp(0.08f, 1f, slack01)
                : 1f;

            for (int i = 1; i < segmentCount - 1; i++)
            {
                Vector3 velocity = currentPositions[i] - previousPositions[i];
                velocity *= damping;

                previousPositions[i] = currentPositions[i];
                currentPositions[i] += velocity;
                currentPositions[i] += Vector3.down * gravity * gravityScale * subDt * subDt;
            }

            int iterationsPerSubstep = Mathf.Max(1, constraintIterations / substeps);
            for (int i = 0; i < iterationsPerSubstep; i++)
            {
                ApplyDistanceConstraints();
                ApplyBendConstraints();
            }

            ApplyTargetShapeFollow(subDt);

            currentPositions[0] = targetPositions[0];
            currentPositions[segmentCount - 1] = targetPositions[segmentCount - 1];
        }
    }

    void ApplyDistanceConstraints()
    {
        currentPositions[0] = targetPositions[0];
        currentPositions[segmentCount - 1] = targetPositions[segmentCount - 1];

        float stretchLimit = 1f + elasticity;
        if (visualMode == RopeVisualMode.TensionedCable)
        {
            stretchLimit = Mathf.Lerp(1.002f, 1f + elasticity, slack01);
        }

        float maxSegmentLength = segmentLength * stretchLimit;
        float minSegmentLength = segmentLength * Mathf.Lerp(0.90f, 0.98f, tautness);

        for (int i = 0; i < segmentCount - 1; i++)
        {
            Vector3 firstPoint = currentPositions[i];
            Vector3 secondPoint = currentPositions[i + 1];
            Vector3 difference = secondPoint - firstPoint;
            float distance = difference.magnitude;

            if (distance < 0.0001f)
            {
                continue;
            }

            Vector3 direction = difference / distance;
            float correctionMagnitude = 0f;

            if (distance > maxSegmentLength)
            {
                correctionMagnitude = distance - maxSegmentLength;
            }
            else if (visualMode == RopeVisualMode.TensionedCable && slack01 < 0.05f && distance < minSegmentLength)
            {
                correctionMagnitude = distance - minSegmentLength;
            }
            else
            {
                continue;
            }

            Vector3 correction = direction * correctionMagnitude;

            bool firstLocked = i == 0;
            bool secondLocked = i + 1 == segmentCount - 1;

            if (!firstLocked && !secondLocked)
            {
                currentPositions[i] += correction * 0.5f;
                currentPositions[i + 1] -= correction * 0.5f;
            }
            else if (firstLocked && !secondLocked)
            {
                currentPositions[i + 1] -= correction;
            }
            else if (!firstLocked && secondLocked)
            {
                currentPositions[i] += correction;
            }
        }
    }

    void ApplyBendConstraints()
    {
        if (bendStiffness <= 0f)
        {
            return;
        }

        float bend = bendStiffness * (visualMode == RopeVisualMode.TensionedCable ? Mathf.Lerp(1f, 0.45f, slack01) : 0.45f);
        bend = Mathf.Clamp01(bend);

        for (int i = 1; i < segmentCount - 1; i++)
        {
            Vector3 expected = (currentPositions[i - 1] + currentPositions[i + 1]) * 0.5f;
            currentPositions[i] = Vector3.Lerp(currentPositions[i], expected, bend * 0.18f);
        }
    }

    void ApplyTargetShapeFollow(float dt)
    {
        if (shapeFollow <= 0f)
        {
            return;
        }

        float follow = 1f - Mathf.Exp(-shapeFollow * 26f * Mathf.Max(dt, 0.001f));
        if (visualMode == RopeVisualMode.TensionedCable)
        {
            follow = Mathf.Lerp(follow, 0.98f, tautness * (1f - slack01));
        }

        for (int i = 1; i < segmentCount - 1; i++)
        {
            currentPositions[i] = Vector3.Lerp(currentPositions[i], targetPositions[i], follow);
        }
    }

    void CalculateTension()
    {
        float normalizedStretch = Mathf.Max(0f, endpointDistance - ropeLength) / Mathf.Max(ropeLength, 0.001f);
        float speedTerm = Mathf.Clamp01(smoothedBucketVelocity.magnitude / 5.0f);
        float tautTerm = 1f - slack01;

        float constraintError = 0f;
        for (int i = 0; i < segmentCount - 1; i++)
        {
            float distance = Vector3.Distance(currentPositions[i], currentPositions[i + 1]);
            constraintError += Mathf.Abs(distance - segmentLength);
        }

        constraintError /= Mathf.Max(segmentCount - 1, 1);
        float constraintTerm = Mathf.Clamp01(constraintError / Mathf.Max(segmentLength * 0.12f, 0.0001f));

        tension = Mathf.Clamp01(tautTerm * 0.62f + speedTerm * 0.30f + normalizedStretch * 8f + constraintTerm * 0.08f);
    }

    void ApplyTensionColor()
    {
        Color finalColor;

        if (tension < mediumTensionThreshold)
        {
            float t = Mathf.InverseLerp(0f, mediumTensionThreshold, tension);
            finalColor = Color.Lerp(relaxedColor, mediumTensionColor, t);
        }
        else
        {
            float t = Mathf.InverseLerp(mediumTensionThreshold, highTensionThreshold, tension);
            finalColor = Color.Lerp(mediumTensionColor, highTensionColor, t);
        }

        for (int i = 0; i < chainRenderers.Length; i++)
        {
            if (chainRenderers[i] != null)
            {
                SetMaterialColor(chainRenderers[i].material, finalColor);
            }
        }
    }

    void DrawNodeObjects()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            if (nodeObjects[i] != null)
            {
                nodeObjects[i].SetActive(showDebugNodes);
                nodeObjects[i].transform.position = currentPositions[i];
            }
        }
    }

    void DrawChainSegments()
    {
        for (int i = 0; i < segmentCount - 1; i++)
        {
            if (chainSegments[i] == null)
            {
                continue;
            }

            Vector3 start = currentPositions[i];
            Vector3 end = currentPositions[i + 1];
            Vector3 middle = (start + end) * 0.5f;
            Vector3 direction = end - start;
            float length = direction.magnitude;

            chainSegments[i].transform.position = middle;

            if (length > 0.001f)
            {
                chainSegments[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }

            chainSegments[i].transform.localScale = new Vector3(chainRadius, length * 0.5f, chainRadius);
        }
    }

    public void SetRopeTypeByIndex(int index)
    {
        index = Mathf.Clamp(index, 0, 2);
        ropeType = (RopeType)index;
        lastPresetRopeType = (RopeType)(-1);
        ApplyRopeType();

        if (chainRenderers != null && chainRenderers.Length > 0)
        {
            ApplyRopeMaterialByType();
        }
    }

    public void SetRopeTypeCotton()
    {
        SetRopeTypeByIndex(0);
    }

    public void SetRopeTypeNylon()
    {
        SetRopeTypeByIndex(1);
    }

    public void SetRopeTypeSteel()
    {
        SetRopeTypeByIndex(2);
    }

    public void ResetRopeNow()
    {
        ResetRopeShape();
    }

    void OnDrawGizmosSelected()
    {
        if (targetPositions == null || targetPositions.Length < 2)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.8f);
        for (int i = 0; i < targetPositions.Length - 1; i++)
        {
            Gizmos.DrawLine(targetPositions[i], targetPositions[i + 1]);
        }
    }
}
