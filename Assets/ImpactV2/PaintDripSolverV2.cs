using UnityEngine;

public class PaintDripSolverV2 : MonoBehaviour
{
    [Header("References")]
    public PaintSurfaceStateV2 surfaceState;
    public CanvasPainter canvasPainter;
    public PaintSurfaceProfile surfaceProfile;

    [Header("Solver Timing")]
    public bool enableDrips = true;
    public float solverInterval = 0.08f;
    public int cellsProcessedPerStep = 6500;

    [Header("Activation Thresholds")]
    public float minThicknessForFlow = 0.16f;
    public float minWetnessForFlow = 0.12f;

    [Header("Flow Strength")]
    public float baseFlowRate = 0.18f;
    public float gravityInfluence = 1.0f;
    public float storedFlowInfluence = 0.85f;
    public float surfaceSlipMultiplier = 1.0f;
    public float maxTransferFraction = 0.22f;
    [Range(0f, 1f)] public float thickPaintDrag = 0.62f;
    public float gravityTrailCoherence = 0.72f;

    [Header("Direction Behavior")]
    public float diagonalBias = 0.35f;
    public float lateralSpreadChance = 0.12f;

    [Header("Visual Drip Marks")]
    public bool drawVisualDrips = true;
    public Color dripColor = new Color(1f, 0f, 0f, 0.28f);
    public float visualRadiusMultiplier = 0.025f;
    public float visualVelocityScale = 0.55f;
    public int visualEveryNTransfers = 3;

    [Header("Runtime Stats")]
    public int totalTransfers;
    public int transfersLastStep;
    public float totalMovedThickness;
    public float movedThicknessLastStep;

    private int scanIndex;
    private float solverTimer;
    private int visualCounter;

    void Awake()
    {
        AutoFindReferences();
    }

    void Update()
    {
        if (!enableDrips)
        {
            return;
        }

        if (surfaceState == null || !surfaceState.HasValidMaps)
        {
            return;
        }

        solverTimer += Time.deltaTime;

        if (solverTimer < solverInterval)
        {
            return;
        }

        float dt = solverTimer;
        solverTimer = 0f;

        StepSolver(dt);
    }

    private void AutoFindReferences()
    {
        if (surfaceState == null)
        {
            surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        }

        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }
    }

    private void StepSolver(float dt)
    {
        int resolution = surfaceState.Resolution;
        int totalCells = resolution * resolution;

        transfersLastStep = 0;
        movedThicknessLastStep = 0f;

        if (totalCells <= 0)
        {
            return;
        }

        int cellsToProcess = Mathf.Clamp(cellsProcessedPerStep, 1, totalCells);

        for (int i = 0; i < cellsToProcess; i++)
        {
            int index = scanIndex;
            scanIndex++;

            if (scanIndex >= totalCells)
            {
                scanIndex = 0;
            }

            int x = index % resolution;
            int y = index / resolution;

            TryProcessCell(x, y, dt);
        }
    }

    private void TryProcessCell(int x, int y, float dt)
    {
        float thickness = surfaceState.GetThicknessAtCell(x, y);
        float wetness = surfaceState.GetWetnessAtCell(x, y);

        if (thickness < minThicknessForFlow || wetness < minWetnessForFlow)
        {
            return;
        }

        Vector2 flowDirection = BuildFlowDirection(x, y);

        if (flowDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        flowDirection.Normalize();

        int targetX;
        int targetY;

        PickTargetCell(x, y, flowDirection, out targetX, out targetY);

        if (!surfaceState.IsValidCellPublic(targetX, targetY))
        {
            return;
        }

        SurfaceFactors factors = BuildSurfaceFactors();

        float excessThickness = Mathf.Max(0f, thickness - minThicknessForFlow);

        float thicknessDrag = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(thickness * thickPaintDrag * 0.08f));
        float wetFlowGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minWetnessForFlow, 1f, wetness));

        float movable =
            excessThickness *
            wetness *
            wetFlowGate *
            thicknessDrag *
            baseFlowRate *
            surfaceSlipMultiplier *
            factors.slip *
            dt;

        movable = Mathf.Min(movable, thickness * maxTransferFraction);

        if (movable <= 0.00001f)
        {
            return;
        }

        float currentTargetThickness = surfaceState.GetThicknessAtCell(targetX, targetY);
        float currentTargetWetness = surfaceState.GetWetnessAtCell(targetX, targetY);
        Vector2 currentFlow = surfaceState.GetFlowAtCell(x, y);

        float wetnessTransfer = wetness * movable / Mathf.Max(thickness, 0.0001f);
        wetnessTransfer = Mathf.Clamp01(wetnessTransfer);

        Vector2 carriedFlow = Vector2.Lerp(
            currentFlow,
            flowDirection * movable * 8f,
            0.65f
        );

        surfaceState.SetCellState(
            x,
            y,
            thickness - movable,
            Mathf.Max(0f, wetness - wetnessTransfer * 0.45f),
            currentFlow * 0.92f
        );

        surfaceState.SetCellState(
            targetX,
            targetY,
            currentTargetThickness + movable,
            Mathf.Clamp01(currentTargetWetness + wetnessTransfer),
            carriedFlow
        );

        totalTransfers++;
        transfersLastStep++;

        totalMovedThickness += movable;
        movedThicknessLastStep += movable;

        DrawVisualDripIfNeeded(x, y, targetX, targetY, flowDirection, movable);
    }

    private struct SurfaceFactors
    {
        public float slip;
        public float absorption;
        public float dripResistance;
    }

    private SurfaceFactors BuildSurfaceFactors()
    {
        SurfaceFactors factors = new SurfaceFactors
        {
            slip = 0.55f,
            absorption = 0.35f,
            dripResistance = 0.55f
        };

        if (surfaceProfile == null)
        {
            return factors;
        }

        factors.absorption = surfaceProfile.absorption;
        factors.dripResistance = surfaceProfile.dripResistance;

        float lowAbsorption = 1f - Mathf.Clamp01(surfaceProfile.absorption);
        float lowResistance = 1f - Mathf.Clamp01(surfaceProfile.dripResistance);
        float lowFriction = 1f - Mathf.Clamp01(surfaceProfile.wetFriction);

        factors.slip =
            0.15f +
            lowAbsorption * 0.45f +
            lowResistance * 0.25f +
            lowFriction * 0.35f;

        return factors;
    }

    private Vector2 BuildFlowDirection(int x, int y)
    {
        Vector2 storedFlow = surfaceState.GetFlowAtCell(x, y);

        Vector2 gravityFlow = GetGravityDirectionOnSurface();

        Vector2 direction =
            gravityFlow * gravityInfluence +
            storedFlow * storedFlowInfluence;

        if (gravityFlow.sqrMagnitude > 0.0001f && storedFlow.sqrMagnitude > 0.0001f)
        {
            direction = Vector2.Lerp(direction, gravityFlow, gravityTrailCoherence * 0.35f);
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = GetThicknessGradientDirection(x, y);
        }

        return direction;
    }

    private Vector2 GetGravityDirectionOnSurface()
    {
        if (surfaceState == null || surfaceState.surfaceTransform == null)
        {
            return Vector2.zero;
        }

        Vector3 localGravity = surfaceState.surfaceTransform.InverseTransformDirection(
            Physics.gravity.normalized
        );

        Vector2 gravityOnSurface = new Vector2(localGravity.x, localGravity.z);

        if (gravityOnSurface.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return gravityOnSurface.normalized;
    }

    private Vector2 GetThicknessGradientDirection(int x, int y)
    {
        float center = surfaceState.GetThicknessAtCell(x, y);

        float left = surfaceState.GetThicknessAtCell(x - 1, y);
        float right = surfaceState.GetThicknessAtCell(x + 1, y);
        float down = surfaceState.GetThicknessAtCell(x, y - 1);
        float up = surfaceState.GetThicknessAtCell(x, y + 1);

        Vector2 gradient = new Vector2(
            center - right + left - center,
            center - up + down - center
        );

        if (gradient.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return gradient.normalized;
    }

    private void PickTargetCell(
        int x,
        int y,
        Vector2 direction,
        out int targetX,
        out int targetY
    )
    {
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);

        int stepX = direction.x >= 0f ? 1 : -1;
        int stepY = direction.y >= 0f ? 1 : -1;

        bool diagonal =
            Random.value < diagonalBias &&
            absX > 0.25f &&
            absY > 0.25f;

        if (diagonal)
        {
            targetX = x + stepX;
            targetY = y + stepY;
            return;
        }

        if (Random.value < lateralSpreadChance)
        {
            if (absX > absY)
            {
                targetX = x;
                targetY = y + stepY;
            }
            else
            {
                targetX = x + stepX;
                targetY = y;
            }

            return;
        }

        if (absX > absY)
        {
            targetX = x + stepX;
            targetY = y;
        }
        else
        {
            targetX = x;
            targetY = y + stepY;
        }
    }

    private void DrawVisualDripIfNeeded(
        int fromX,
        int fromY,
        int targetX,
        int targetY,
        Vector2 flowDirection,
        float movedAmount
    )
    {
        if (!drawVisualDrips || canvasPainter == null)
        {
            return;
        }

        visualCounter++;

        if (visualCounter % Mathf.Max(1, visualEveryNTransfers) != 0)
        {
            return;
        }

        if (!surfaceState.TryGetWorldPositionFromCell(targetX, targetY, out Vector3 worldPoint))
        {
            return;
        }

        Vector3 worldDirection =
            surfaceState.surfaceTransform.TransformDirection(
                new Vector3(flowDirection.x, 0f, flowDirection.y)
            );

        float radius =
            visualRadiusMultiplier *
            Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(movedAmount * 12f));

        canvasPainter.PaintImpactAtWorldPosition(
            worldPoint,
            worldDirection * visualVelocityScale,
            dripColor,
            radius
        );
    }

    public void ResetSolver()
    {
        totalTransfers = 0;
        transfersLastStep = 0;
        totalMovedThickness = 0f;
        movedThicknessLastStep = 0f;
        scanIndex = 0;
        solverTimer = 0f;
        visualCounter = 0;
    }
}