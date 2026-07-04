using UnityEngine;

public class PaintFilmFluidSolverV2 : MonoBehaviour
{
    [Header("References")]
    public PaintSurfaceStateV2 surfaceState;
    public PaintSurfaceProfile surfaceProfile;

    [Header("Real Surface Fluid Film")]
    public bool enableFluidFilm = true;
    public bool solveWholeFilm = true;
    public float solverInterval = 0.035f;
    [Range(1, 6)] public int subSteps = 3;
    public int maxCellsPerFrame = 65536;

    [Header("Activation")]
    public float minThicknessToMove = 0.025f;
    public float minWetnessToMove = 0.025f;
    public float restingFilmThickness = 0.006f;

    [Header("Paint Material Behavior")]
    public float viscosity = 5.5f;
    public float density = 1.0f;
    public float pressureStrength = 2.4f;
    public float gravityStrength = 2.6f;
    public float surfaceTension = 0.42f;
    public float adhesion = 0.34f;
    public float wetFriction = 0.58f;
    public float maxTransferFractionPerSubstep = 0.18f;

    [Header("Cohesion / Smoothing")]
    public float velocityDamping = 0.74f;
    public float flowMemory = 0.82f;
    public float edgeCling = 0.18f;
    public float capillarySpread = 0.18f;

    [Header("Runtime Stats")]
    public int activeFluidCells;
    public int movedCellsLastStep;
    public float movedMassLastStep;
    public float totalMovedMass;
    public float averageFilmSpeed;
    public Vector2 surfaceGravityDirection;

    private float solverTimer;
    private float[] nextThickness;
    private float[] nextWetness;
    private float[] nextFlowX;
    private float[] nextFlowY;
    private int allocatedResolution;

    private static readonly Vector2Int[] CardinalNeighbours = new Vector2Int[]
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    void Awake()
    {
        AutoFindReferences();
    }

    void Update()
    {
        if (!enableFluidFilm)
        {
            return;
        }

        AutoFindReferences();

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

        StepFluid(dt);
    }

    private void AutoFindReferences()
    {
        if (surfaceState == null)
        {
            surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        }
    }

    private void EnsureBuffers()
    {
        if (surfaceState == null)
        {
            return;
        }

        int resolution = surfaceState.Resolution;
        int size = resolution * resolution;

        if (allocatedResolution == resolution && nextThickness != null && nextThickness.Length == size)
        {
            return;
        }

        allocatedResolution = resolution;
        nextThickness = new float[size];
        nextWetness = new float[size];
        nextFlowX = new float[size];
        nextFlowY = new float[size];
    }

    public void StepFluid(float dt)
    {
        EnsureBuffers();

        float[] thickness = surfaceState.ThicknessMapRaw;
        float[] wetness = surfaceState.WetnessMapRaw;
        float[] flowX = surfaceState.FlowXMapRaw;
        float[] flowY = surfaceState.FlowYMapRaw;

        if (thickness == null || wetness == null || flowX == null || flowY == null)
        {
            return;
        }

        int resolution = surfaceState.Resolution;
        int size = resolution * resolution;

        if (size <= 0)
        {
            return;
        }

        movedCellsLastStep = 0;
        movedMassLastStep = 0f;
        activeFluidCells = 0;
        averageFilmSpeed = 0f;
        surfaceGravityDirection = GetGravityDirectionOnSurface();

        float subDt = dt / Mathf.Max(1, subSteps);
        float speedSumThisStep = 0f;
        int speedSamplesThisStep = 0;

        for (int step = 0; step < Mathf.Max(1, subSteps); step++)
        {
            System.Array.Copy(thickness, nextThickness, size);
            System.Array.Copy(wetness, nextWetness, size);
            System.Array.Copy(flowX, nextFlowX, size);
            System.Array.Copy(flowY, nextFlowY, size);

            SolveSubStep(thickness, wetness, flowX, flowY, resolution, subDt, ref speedSumThisStep, ref speedSamplesThisStep);

            float[] swapT = thickness;
            float[] swapW = wetness;
            float[] swapX = flowX;
            float[] swapY = flowY;

            thickness = nextThickness;
            wetness = nextWetness;
            flowX = nextFlowX;
            flowY = nextFlowY;

            nextThickness = swapT;
            nextWetness = swapW;
            nextFlowX = swapX;
            nextFlowY = swapY;
        }

        surfaceState.ReplaceMapsFromSolver(thickness, wetness, flowX, flowY);
        surfaceState.RecalculateStatsNow();

        averageFilmSpeed = speedSamplesThisStep > 0 ? speedSumThisStep / speedSamplesThisStep : 0f;
        totalMovedMass += movedMassLastStep;
    }

    private void SolveSubStep(
        float[] thickness,
        float[] wetness,
        float[] flowX,
        float[] flowY,
        int resolution,
        float dt,
        ref float speedSumThisStep,
        ref int speedSamplesThisStep
    )
    {
        int totalCells = resolution * resolution;
        int processed = 0;
        int processLimit = solveWholeFilm ? totalCells : Mathf.Clamp(maxCellsPerFrame, 1, totalCells);

        for (int index = 0; index < totalCells; index++)
        {
            if (processed >= processLimit)
            {
                break;
            }

            float h = thickness[index];
            float w = wetness[index];

            if (h <= minThicknessToMove || w <= minWetnessToMove)
            {
                continue;
            }

            processed++;
            activeFluidCells++;

            int x = index % resolution;
            int y = index / resolution;

            Vector2 inheritedFlow = new Vector2(flowX[index], flowY[index]);
            float mobility = ComputeMobility(h, w);
            float available = Mathf.Max(0f, h - restingFilmThickness);
            float maxTransfer = available * maxTransferFractionPerSubstep;
            float movedFromCell = 0f;
            Vector2 accumulatedFlow = inheritedFlow * velocityDamping;

            for (int n = 0; n < CardinalNeighbours.Length; n++)
            {
                int nx = x + CardinalNeighbours[n].x;
                int ny = y + CardinalNeighbours[n].y;

                if (!IsValidCell(nx, ny, resolution))
                {
                    continue;
                }

                int ni = ny * resolution + nx;
                Vector2 dir = new Vector2(CardinalNeighbours[n].x, CardinalNeighbours[n].y);

                float nh = thickness[ni];
                float nw = wetness[ni];

                float heightGradient = h - nh;
                float wetGradient = Mathf.Max(0f, w - nw);
                float gravityDrive = Vector2.Dot(surfaceGravityDirection, dir) * gravityStrength;
                float pressureDrive = heightGradient * pressureStrength;
                float capillaryDrive = wetGradient * capillarySpread;
                float tensionDrive = ComputeSurfaceTensionDrive(x, y, nx, ny, thickness, resolution);

                float drive = pressureDrive + gravityDrive + capillaryDrive + tensionDrive;

                if (drive <= 0f)
                {
                    continue;
                }

                float edgeResistance = 1f;
                if (nx <= 0 || nx >= resolution - 1 || ny <= 0 || ny >= resolution - 1)
                {
                    edgeResistance += edgeCling;
                }

                float transfer = drive * mobility * dt / edgeResistance;
                transfer = Mathf.Min(transfer, maxTransfer - movedFromCell);
                transfer = Mathf.Min(transfer, available - movedFromCell);

                if (transfer <= 0.000001f)
                {
                    continue;
                }

                float wetTransfer = Mathf.Clamp01(w * transfer / Mathf.Max(h, 0.0001f));

                nextThickness[index] = Mathf.Max(0f, nextThickness[index] - transfer);
                nextThickness[ni] += transfer;

                nextWetness[index] = Mathf.Clamp01(nextWetness[index] - wetTransfer * 0.36f);
                nextWetness[ni] = Mathf.Clamp01(nextWetness[ni] + wetTransfer * 0.92f);

                Vector2 transferFlow = dir.normalized * transfer * 14f;
                accumulatedFlow += transferFlow;

                Vector2 oldNeighbourFlow = new Vector2(nextFlowX[ni], nextFlowY[ni]);
                Vector2 blendedNeighbourFlow = Vector2.Lerp(oldNeighbourFlow, transferFlow, flowMemory);
                nextFlowX[ni] = blendedNeighbourFlow.x;
                nextFlowY[ni] = blendedNeighbourFlow.y;

                movedFromCell += transfer;
                movedMassLastStep += transfer;
                movedCellsLastStep++;

                if (movedFromCell >= maxTransfer)
                {
                    break;
                }
            }

            Vector2 finalFlow = Vector2.Lerp(inheritedFlow, accumulatedFlow, flowMemory);
            nextFlowX[index] = finalFlow.x;
            nextFlowY[index] = finalFlow.y;
            speedSumThisStep += finalFlow.magnitude;
            speedSamplesThisStep++;
        }
    }

    private float ComputeMobility(float thickness, float wetness)
    {
        SurfaceFactors factors = BuildSurfaceFactors();

        float effectiveViscosity = viscosity * Mathf.Lerp(0.55f, 1.45f, factors.viscosityMultiplier);
        float effectiveFriction = Mathf.Clamp01(wetFriction * factors.frictionMultiplier);
        float effectiveAdhesion = Mathf.Clamp01(adhesion * factors.adhesionMultiplier);

        float wetGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minWetnessToMove, 1f, wetness));
        float thickDrag = 1f + thickness * effectiveViscosity * 0.12f;
        float surfaceDrag = 1f + effectiveFriction * 1.4f + effectiveAdhesion * 1.2f;

        return density * wetGate / Mathf.Max(0.0001f, thickDrag * surfaceDrag);
    }

    private float ComputeSurfaceTensionDrive(
        int x,
        int y,
        int nx,
        int ny,
        float[] thickness,
        int resolution
    )
    {
        float centerCurvature = ComputeLocalCurvature(x, y, thickness, resolution);
        float neighbourCurvature = ComputeLocalCurvature(nx, ny, thickness, resolution);
        return (centerCurvature - neighbourCurvature) * surfaceTension;
    }

    private float ComputeLocalCurvature(int x, int y, float[] thickness, int resolution)
    {
        int index = y * resolution + x;
        float center = thickness[index];
        float sum = 0f;
        int count = 0;

        for (int n = 0; n < CardinalNeighbours.Length; n++)
        {
            int nx = x + CardinalNeighbours[n].x;
            int ny = y + CardinalNeighbours[n].y;

            if (!IsValidCell(nx, ny, resolution))
            {
                continue;
            }

            sum += thickness[ny * resolution + nx];
            count++;
        }

        if (count == 0)
        {
            return 0f;
        }

        return center - sum / count;
    }

    private Vector2 GetGravityDirectionOnSurface()
    {
        if (surfaceState == null || surfaceState.surfaceTransform == null)
        {
            return Vector2.zero;
        }

        Vector3 localGravity = surfaceState.surfaceTransform.InverseTransformDirection(Physics.gravity.normalized);
        Vector2 g = new Vector2(localGravity.x, localGravity.z);

        if (g.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return g.normalized;
    }

    private bool IsValidCell(int x, int y, int resolution)
    {
        return x >= 0 && x < resolution && y >= 0 && y < resolution;
    }

    private struct SurfaceFactors
    {
        public float viscosityMultiplier;
        public float frictionMultiplier;
        public float adhesionMultiplier;
    }

    private SurfaceFactors BuildSurfaceFactors()
    {
        SurfaceFactors factors = new SurfaceFactors
        {
            viscosityMultiplier = 1f,
            frictionMultiplier = 1f,
            adhesionMultiplier = 1f
        };

        if (surfaceProfile == null)
        {
            return factors;
        }

        factors.viscosityMultiplier = Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(surfaceProfile.dripResistance));
        factors.frictionMultiplier = Mathf.Lerp(0.65f, 1.35f, Mathf.Clamp01(surfaceProfile.wetFriction));
        factors.adhesionMultiplier = Mathf.Lerp(0.55f, 1.45f, Mathf.Clamp01(surfaceProfile.absorption));

        return factors;
    }

    public void ResetSolver()
    {
        solverTimer = 0f;
        activeFluidCells = 0;
        movedCellsLastStep = 0;
        movedMassLastStep = 0f;
        totalMovedMass = 0f;
        averageFilmSpeed = 0f;
    }
}
