using UnityEngine;

public class PaintSurfaceStateV2 : MonoBehaviour
{
    [Header("Surface Reference")]
    public Transform surfaceTransform;

    [Header("Map Settings")]
    public int mapResolution = 256;
    public float thicknessScale = 65f;
    public float wetnessDepositScale = 1f;
    public float flowMemory = 0.7f;

    [Header("Drying / Absorption")]
    public bool enableDrying = true;
    public float dryingUpdateInterval = 0.1f;
    public float baseDryingStrength = 0.18f;
    public float absorptionWetnessLoss = 0.35f;
    public float thickPaintDryingResistance = 4f;

    [Header("Coverage Thresholds")]
    public float thicknessCoverageThreshold = 0.008f;
    public float wetnessCoverageThreshold = 0.04f;

    [Header("Fluid Film Rendering")]
    [Tooltip("This project now renders paint as a 2D surface-fluid film with texture/normal maps, not as raised mesh geometry.")]
    public bool autoCreateRaisedPaintRenderer = false;
    public PaintThicknessRendererV2 raisedPaintRenderer;
    public bool autoLinkCanvasFluidRenderer = true;
    public int dataRevision;

    [Header("Thick Paint Deposition Model")]
    public bool enableRaisedRimMass = true;
    [Range(0f, 0.6f)] public float raisedRimMassStrength = 0.22f;
    [Range(0.8f, 2.8f)] public float centralMassPower = 1.35f;
    public float maxStableCellThickness = 32f;

    [Header("Runtime Stats")]
    public int totalImpacts;
    public int lastDepositedCells;
    public float totalDepositedMass;
    public float maxThickness;
    public float averageThickness;
    public float thickCoverage01;
    public float wetCoverage01;
    public float averageWetness;

    private float[] thicknessMap;
    private float[] wetnessMap;
    private float[] flowXMap;
    private float[] flowYMap;

    private float dryingTimer;
    private float statsTimer;

    private PaintSurfaceProfile activeSurfaceProfile;

    void Awake()
    {
        if (surfaceTransform == null)
        {
            surfaceTransform = transform;
        }

        InitializeMaps();
        // Raised mesh rendering is intentionally disabled. The upgraded system uses a
        // real surface-fluid height field plus texture/normal-map rendering instead.
        DisableLegacyRaisedPaintRenderer();
        LinkCanvasFluidRenderer();
    }

    void Update()
    {
        if (enableDrying)
        {
            dryingTimer += Time.deltaTime;

            if (dryingTimer >= dryingUpdateInterval)
            {
                float dt = dryingTimer;
                dryingTimer = 0f;
                StepDrying(dt);
            }
        }

        statsTimer += Time.deltaTime;

        if (statsTimer >= 0.25f)
        {
            statsTimer = 0f;
            RecalculateStats();
        }
    }

    private void InitializeMaps()
    {
        mapResolution = Mathf.Max(32, mapResolution);

        int size = mapResolution * mapResolution;

        thicknessMap = new float[size];
        wetnessMap = new float[size];
        flowXMap = new float[size];
        flowYMap = new float[size];

        ResetState();
    }

    public void RegisterImpact(
        PaintImpactData impact,
        PaintImpactType impactType,
        PaintSurfaceProfile surfaceProfile
    )
    {
        if (thicknessMap == null || wetnessMap == null)
        {
            InitializeMaps();
        }

        activeSurfaceProfile = surfaceProfile;

        if (!WorldToMapCell(impact.worldPosition, out int centerX, out int centerY))
        {
            return;
        }

        SurfaceRuntimeFactors factors = BuildFactors(surfaceProfile);

        float baseRadiusWorld = Mathf.Max(impact.particleRadius, 0.003f);
        float radiusMultiplier = GetRadiusMultiplier(impactType, factors);
        int radiusCells = WorldRadiusToCells(baseRadiusWorld * radiusMultiplier);

        radiusCells = Mathf.Clamp(radiusCells, 1, mapResolution / 5);

        float depositedMass =
            Mathf.Max(impact.particleMass, 0.0001f)
            * thicknessScale
            * GetMassMultiplier(impactType)
            * Mathf.Lerp(1.15f, 0.65f, factors.absorption);

        float depositedWetness =
            impact.wetness
            * wetnessDepositScale
            * GetWetnessMultiplier(impactType)
            * Mathf.Lerp(1.1f, 0.35f, factors.absorption);

        Vector2 localFlow = GetLocalFlowDirection(impact);
        float flowStrength = impact.TangentialSpeed * Mathf.Lerp(0.35f, 1.4f, factors.smear);

        int minX = Mathf.Max(centerX - radiusCells, 0);
        int maxX = Mathf.Min(centerX + radiusCells, mapResolution - 1);
        int minY = Mathf.Max(centerY - radiusCells, 0);
        int maxY = Mathf.Min(centerY + radiusCells, mapResolution - 1);

        int depositedCells = 0;
        int noiseSeed = impact.sourceId * 97 + totalImpacts * 17;

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float noise = Mathf.PerlinNoise(
                    (x + noiseSeed) * 0.11f,
                    (y + noiseSeed) * 0.11f
                );

                float irregularRadius = radiusCells * (1f + (noise - 0.5f) * 0.45f * factors.edgeNoise);

                if (distance > irregularRadius)
                {
                    continue;
                }

                float normalized = distance / Mathf.Max(irregularRadius, 0.0001f);
                float falloff = Mathf.Clamp01(1f - normalized);

                float coreFalloff = Mathf.Pow(falloff, centralMassPower);
                float rimFalloff = 0f;

                if (enableRaisedRimMass)
                {
                    float rimCenter = 0.72f;
                    float rimWidth = 0.18f;
                    float rimDistance = Mathf.Abs(normalized - rimCenter) / rimWidth;
                    rimFalloff = Mathf.Clamp01(1f - rimDistance);
                    rimFalloff *= rimFalloff * raisedRimMassStrength * Mathf.Lerp(1.15f, 0.35f, factors.absorption);
                }

                float massFalloff = Mathf.Clamp01(coreFalloff + rimFalloff);
                float wetnessFalloff = Mathf.Sqrt(falloff);

                int index = y * mapResolution + x;

                float localMass = depositedMass * massFalloff;
                float localWetness = depositedWetness * wetnessFalloff;

                thicknessMap[index] = Mathf.Min(maxStableCellThickness, thicknessMap[index] + localMass);
                wetnessMap[index] = Mathf.Clamp01(wetnessMap[index] + localWetness);

                Vector2 oldFlow = new Vector2(flowXMap[index], flowYMap[index]);
                Vector2 newFlow = localFlow * flowStrength * falloff;
                Vector2 blendedFlow = Vector2.Lerp(oldFlow, newFlow, flowMemory);

                flowXMap[index] = blendedFlow.x;
                flowYMap[index] = blendedFlow.y;

                totalDepositedMass += localMass;

                if (thicknessMap[index] > maxThickness)
                {
                    maxThickness = thicknessMap[index];
                }

                depositedCells++;
            }
        }

        totalImpacts++;
        lastDepositedCells = depositedCells;
        MarkDataChanged();
    }

    private struct SurfaceRuntimeFactors
    {
        public float absorption;
        public float spread;
        public float smear;
        public float dryingSpeed;
        public float edgeNoise;
    }

    private SurfaceRuntimeFactors BuildFactors(PaintSurfaceProfile profile)
    {
        SurfaceRuntimeFactors factors = new SurfaceRuntimeFactors
        {
            absorption = 0.35f,
            spread = 1.0f,
            smear = 1.0f,
            dryingSpeed = 0.5f,
            edgeNoise = 1.0f
        };

        if (profile == null)
        {
            return factors;
        }

        factors.absorption = profile.absorption;
        factors.spread = profile.spreadFactor;
        factors.smear = profile.smearFactor;
        factors.dryingSpeed = profile.dryingSpeed;
        factors.edgeNoise = profile.edgeNoise;

        return factors;
    }

    private float GetRadiusMultiplier(PaintImpactType impactType, SurfaceRuntimeFactors factors)
    {
        float multiplier = 1f;

        switch (impactType)
        {
            case PaintImpactType.SoftDeposit:
                multiplier = 0.75f;
                break;

            case PaintImpactType.NormalSplat:
                multiplier = 1.0f;
                break;

            case PaintImpactType.HardSplash:
                multiplier = 1.35f;
                break;

            case PaintImpactType.GrazingSmear:
                multiplier = 1.15f;
                break;

            case PaintImpactType.MistImpact:
                multiplier = 0.55f;
                break;

            case PaintImpactType.HeavyBlob:
                multiplier = 1.65f;
                break;

            case PaintImpactType.SkidImpact:
                multiplier = 1.25f;
                break;
        }

        multiplier *= Mathf.Lerp(0.75f, 1.45f, Mathf.Clamp01(factors.spread / 2f));
        multiplier *= Mathf.Lerp(0.9f, 1.25f, 1f - factors.absorption);

        return multiplier;
    }

    private float GetMassMultiplier(PaintImpactType impactType)
    {
        switch (impactType)
        {
            case PaintImpactType.SoftDeposit:
                return 0.8f;

            case PaintImpactType.NormalSplat:
                return 1.0f;

            case PaintImpactType.HardSplash:
                return 0.9f;

            case PaintImpactType.GrazingSmear:
                return 0.75f;

            case PaintImpactType.MistImpact:
                return 0.35f;

            case PaintImpactType.HeavyBlob:
                return 2.2f;

            case PaintImpactType.SkidImpact:
                return 0.65f;
        }

        return 1f;
    }

    private float GetWetnessMultiplier(PaintImpactType impactType)
    {
        switch (impactType)
        {
            case PaintImpactType.SoftDeposit:
                return 0.75f;

            case PaintImpactType.NormalSplat:
                return 1.0f;

            case PaintImpactType.HardSplash:
                return 1.25f;

            case PaintImpactType.GrazingSmear:
                return 1.15f;

            case PaintImpactType.MistImpact:
                return 0.65f;

            case PaintImpactType.HeavyBlob:
                return 1.6f;

            case PaintImpactType.SkidImpact:
                return 1.35f;
        }

        return 1f;
    }

    private Vector2 GetLocalFlowDirection(PaintImpactData impact)
    {
        Vector3 localVelocity = surfaceTransform.InverseTransformDirection(impact.TangentialVelocity);
        Vector2 flow = new Vector2(localVelocity.x, localVelocity.z);

        if (flow.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return flow.normalized;
    }

    private bool WorldToMapCell(Vector3 worldPosition, out int x, out int y)
    {
        Vector3 localPoint = surfaceTransform.InverseTransformPoint(worldPosition);

        float u = localPoint.x + 0.5f;
        float v = localPoint.z + 0.5f;

        x = 0;
        y = 0;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return false;
        }

        x = Mathf.RoundToInt(u * (mapResolution - 1));
        y = Mathf.RoundToInt(v * (mapResolution - 1));

        return true;
    }

    private int WorldRadiusToCells(float worldRadius)
    {
        float largestSide = Mathf.Max(surfaceTransform.lossyScale.x, surfaceTransform.lossyScale.z);
        int cells = Mathf.RoundToInt((worldRadius / largestSide) * mapResolution);
        return Mathf.Max(cells, 1);
    }

    private void StepDrying(float dt)
    {
        SurfaceRuntimeFactors factors = BuildFactors(activeSurfaceProfile);
        bool changed = false;

        float drying = baseDryingStrength * Mathf.Max(0.05f, factors.dryingSpeed);
        float absorption = absorptionWetnessLoss * Mathf.Clamp01(factors.absorption);

        for (int i = 0; i < wetnessMap.Length; i++)
        {
            float wetness = wetnessMap[i];

            if (wetness <= 0f)
            {
                continue;
            }

            float thickness = thicknessMap[i];
            float resistance = 1f + thickness * thickPaintDryingResistance;
            float wetnessLoss = (drying + absorption) * dt / resistance;

            float newWetness = Mathf.Max(0f, wetness - wetnessLoss);

            if (Mathf.Abs(newWetness - wetness) > 0.00001f)
            {
                changed = true;
            }

            wetnessMap[i] = newWetness;

            if (wetnessMap[i] <= 0.001f)
            {
                wetnessMap[i] = 0f;
                flowXMap[i] *= 0.5f;
                flowYMap[i] *= 0.5f;
            }
        }

        if (changed)
        {
            MarkDataChanged();
        }
    }

    private void RecalculateStats()
    {
        if (thicknessMap == null || wetnessMap == null)
        {
            return;
        }

        float thicknessSum = 0f;
        float wetnessSum = 0f;

        int thickCells = 0;
        int wetCells = 0;

        float localMaxThickness = 0f;

        for (int i = 0; i < thicknessMap.Length; i++)
        {
            float thickness = thicknessMap[i];
            float wetness = wetnessMap[i];

            thicknessSum += thickness;
            wetnessSum += wetness;

            if (thickness > localMaxThickness)
            {
                localMaxThickness = thickness;
            }

            if (thickness > thicknessCoverageThreshold)
            {
                thickCells++;
            }

            if (wetness > wetnessCoverageThreshold)
            {
                wetCells++;
            }
        }

        int totalCells = thicknessMap.Length;

        averageThickness = thicknessSum / totalCells;
        averageWetness = wetnessSum / totalCells;
        maxThickness = localMaxThickness;
        thickCoverage01 = thickCells / (float)totalCells;
        wetCoverage01 = wetCells / (float)totalCells;
    }

    public void ResetState()
    {
        if (thicknessMap == null || wetnessMap == null)
        {
            return;
        }

        for (int i = 0; i < thicknessMap.Length; i++)
        {
            thicknessMap[i] = 0f;
            wetnessMap[i] = 0f;
            flowXMap[i] = 0f;
            flowYMap[i] = 0f;
        }

        totalImpacts = 0;
        lastDepositedCells = 0;
        totalDepositedMass = 0f;
        maxThickness = 0f;
        averageThickness = 0f;
        thickCoverage01 = 0f;
        wetCoverage01 = 0f;
        averageWetness = 0f;
        MarkDataChanged();

        dryingTimer = 0f;
        statsTimer = 0f;
    }


    public float[] ThicknessMapRaw
    {
        get { return thicknessMap; }
    }

    public float[] WetnessMapRaw
    {
        get { return wetnessMap; }
    }

    public float[] FlowXMapRaw
    {
        get { return flowXMap; }
    }

    public float[] FlowYMapRaw
    {
        get { return flowYMap; }
    }

    public void ReplaceMapsFromSolver(
        float[] newThickness,
        float[] newWetness,
        float[] newFlowX,
        float[] newFlowY
    )
    {
        if (newThickness == null || newWetness == null || newFlowX == null || newFlowY == null)
        {
            return;
        }

        int expectedSize = mapResolution * mapResolution;

        if (newThickness.Length != expectedSize ||
            newWetness.Length != expectedSize ||
            newFlowX.Length != expectedSize ||
            newFlowY.Length != expectedSize)
        {
            return;
        }

        thicknessMap = newThickness;
        wetnessMap = newWetness;
        flowXMap = newFlowX;
        flowYMap = newFlowY;

        MarkDataChanged();
    }

    public void RecalculateStatsNow()
    {
        RecalculateStats();
    }

    public int Resolution
    {
        get
        {
            return mapResolution;
        }
    }

    public bool HasValidMaps
    {
        get
        {
            return thicknessMap != null &&
                   wetnessMap != null &&
                   flowXMap != null &&
                   flowYMap != null;
        }
    }

    public bool IsValidCellPublic(int x, int y)
    {
        return x >= 0 && x < mapResolution && y >= 0 && y < mapResolution;
    }

    public float GetThicknessAtCell(int x, int y)
    {
        if (!IsValidCellPublic(x, y) || thicknessMap == null)
        {
            return 0f;
        }

        return thicknessMap[y * mapResolution + x];
    }

    public float GetWetnessAtCell(int x, int y)
    {
        if (!IsValidCellPublic(x, y) || wetnessMap == null)
        {
            return 0f;
        }

        return wetnessMap[y * mapResolution + x];
    }

    public Vector2 GetFlowAtCell(int x, int y)
    {
        if (!IsValidCellPublic(x, y) || flowXMap == null || flowYMap == null)
        {
            return Vector2.zero;
        }

        int index = y * mapResolution + x;
        return new Vector2(flowXMap[index], flowYMap[index]);
    }

    public void SetCellState(
        int x,
        int y,
        float thickness,
        float wetness,
        Vector2 flow
    )
    {
        if (!IsValidCellPublic(x, y) || thicknessMap == null || wetnessMap == null)
        {
            return;
        }

        int index = y * mapResolution + x;

        thicknessMap[index] = Mathf.Clamp(thickness, 0f, maxStableCellThickness);
        wetnessMap[index] = Mathf.Clamp01(wetness);

        flowXMap[index] = flow.x;
        flowYMap[index] = flow.y;

        MarkDataChanged();
    }

    public void AddToCell(
        int x,
        int y,
        float addedThickness,
        float addedWetness,
        Vector2 addedFlow,
        float flowBlend = 0.55f
    )
    {
        if (!IsValidCellPublic(x, y) || thicknessMap == null || wetnessMap == null)
        {
            return;
        }

        int index = y * mapResolution + x;

        thicknessMap[index] = Mathf.Clamp(thicknessMap[index] + addedThickness, 0f, maxStableCellThickness);
        wetnessMap[index] = Mathf.Clamp01(wetnessMap[index] + addedWetness);

        Vector2 oldFlow = new Vector2(flowXMap[index], flowYMap[index]);
        Vector2 blendedFlow = Vector2.Lerp(oldFlow, addedFlow, flowBlend);

        flowXMap[index] = blendedFlow.x;
        flowYMap[index] = blendedFlow.y;

        MarkDataChanged();
    }

    public bool TryGetWorldPositionFromCell(int x, int y, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!IsValidCellPublic(x, y) || surfaceTransform == null)
        {
            return false;
        }

        float u = x / (float)(mapResolution - 1);
        float v = y / (float)(mapResolution - 1);

        Vector3 localPoint = new Vector3(
            u - 0.5f,
            0f,
            v - 0.5f
        );

        worldPosition = surfaceTransform.TransformPoint(localPoint);
        return true;
    }


    private void LinkCanvasFluidRenderer()
    {
        if (!autoLinkCanvasFluidRenderer)
        {
            return;
        }

        CanvasPainter painter = GetComponent<CanvasPainter>();

        if (painter == null)
        {
            painter = Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (painter == null)
        {
            return;
        }

        painter.fluidSurfaceState = this;
        painter.renderFluidFilmFromSurfaceState = true;
    }

    private void DisableLegacyRaisedPaintRenderer()
    {
        if (raisedPaintRenderer == null)
        {
            raisedPaintRenderer = GetComponent<PaintThicknessRendererV2>();
        }

        if (raisedPaintRenderer != null)
        {
            raisedPaintRenderer.enableRaisedPaintMesh = false;
            raisedPaintRenderer.enabled = false;
        }
    }

    public void MarkDataChanged()
    {
        dataRevision++;
    }

    public bool TryGetCellFromWorldPosition(Vector3 worldPosition, out int x, out int y)
    {
        return WorldToMapCell(worldPosition, out x, out y);
    }

    public float SampleThicknessBilinear(float u, float v)
    {
        return SampleMapBilinear(thicknessMap, u, v);
    }

    public float SampleWetnessBilinear(float u, float v)
    {
        return SampleMapBilinear(wetnessMap, u, v);
    }

    private float SampleMapBilinear(float[] map, float u, float v)
    {
        if (map == null || mapResolution <= 1)
        {
            return 0f;
        }

        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        float fx = u * (mapResolution - 1);
        float fy = v * (mapResolution - 1);

        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int x1 = Mathf.Min(x0 + 1, mapResolution - 1);
        int y1 = Mathf.Min(y0 + 1, mapResolution - 1);

        float tx = fx - x0;
        float ty = fy - y0;

        float a = map[y0 * mapResolution + x0];
        float b = map[y0 * mapResolution + x1];
        float c = map[y1 * mapResolution + x0];
        float d = map[y1 * mapResolution + x1];

        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    public float GetNormalizedThicknessAtCell(int x, int y, float sensitivity = 0.28f)
    {
        float thickness = GetThicknessAtCell(x, y);
        return 1f - Mathf.Exp(-Mathf.Max(0f, thickness) * sensitivity);
    }

}