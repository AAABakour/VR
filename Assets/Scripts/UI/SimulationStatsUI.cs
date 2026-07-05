using TMPro;
using UnityEngine;

public class SimulationStatsUI : MonoBehaviour
{
    [Header("References")]
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;
    public PaintImpactEngineV2 impactEngine;
    public PaintSurfaceStateV2 surfaceState;
    public PaintDripSolverV2 dripSolver;
    public PaintFilmFluidSolverV2 fluidFilmSolver;
    public PaintThicknessRendererV2 thicknessRenderer;
    public RigRopeController ropeController;
    public TextMeshProUGUI outputText;

    [Header("Update Settings")]
    public float updateInterval = 0.25f;

    private float updateTimer;
    private float fpsTimer;
    private int frameCounter;
    private float displayedFps;

    void Awake()
    {
        AutoFindReferences();

        if (outputText == null)
        {
            outputText = GetComponent<TextMeshProUGUI>();
        }
    }

    private void AutoFindReferences()
    {
        if (paintEmitter == null)
        {
            paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (particleSimulator == null)
        {
            particleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        }

        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (impactEngine == null)
        {
            impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        }

        if (surfaceState == null)
        {
            surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        }

        if (dripSolver == null)
        {
            dripSolver = Object.FindFirstObjectByType<PaintDripSolverV2>();
        }

        if (fluidFilmSolver == null)
        {
            fluidFilmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        }

        if (thicknessRenderer == null)
        {
            thicknessRenderer = Object.FindFirstObjectByType<PaintThicknessRendererV2>();
        }

        if (ropeController == null)
        {
            ropeController = Object.FindFirstObjectByType<RigRopeController>();
        }
    }

    void Update()
    {
        UpdateFpsCounter();

        updateTimer += Time.unscaledDeltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            AutoFindReferences();
            RefreshText();
        }
    }

    private void UpdateFpsCounter()
    {
        frameCounter++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= 0.5f)
        {
            displayedFps = frameCounter / fpsTimer;
            frameCounter = 0;
            fpsTimer = 0f;
        }
    }

    private void RefreshText()
    {
        if (outputText == null)
        {
            return;
        }

        int activeParticles = 0;
        int visibleDroplets = 0;

        if (particleSimulator != null)
        {
            activeParticles = particleSimulator.ActiveParticleCount;
            visibleDroplets = particleSimulator.ActiveVisualDropletCount;
        }

        float remainingPaint = 0f;
        float paintPercent = 0f;
        string nozzleShape = "-";

        if (paintEmitter != null)
        {
            remainingPaint = paintEmitter.RemainingPaintAmount;
            paintPercent = paintEmitter.PaintFill01 * 100f;
            nozzleShape = paintEmitter.CurrentNozzleShapeName;
        }

        string surfaceName = "-";

        if (impactEngine != null && !string.IsNullOrEmpty(impactEngine.lastSurfaceName))
        {
            surfaceName = impactEngine.lastSurfaceName;
        }
        else if (canvasPainter != null)
        {
            surfaceName = canvasPainter.surfaceType.ToString();
        }

        string impactLine = "Impacts: -";

        if (impactEngine != null)
        {
            impactLine =
                "Impacts: " + impactEngine.totalImpacts +
                " | Last: " + impactEngine.lastImpactType +
                " | Speed: " + impactEngine.lastImpactSpeed.ToString("0.00");
        }

        string surfaceLine = "Surface State: -";

        if (surfaceState != null)
        {
            surfaceLine =
                "Coverage: " + (surfaceState.thickCoverage01 * 100f).ToString("0") + "%" +
                " | Wet: " + (surfaceState.wetCoverage01 * 100f).ToString("0") + "%" +
                " | MaxThick: " + surfaceState.maxThickness.ToString("0.000");
        }

        string dripLine = "Drips: -";

        if (dripSolver != null)
        {
            dripLine =
                "Drips: " + dripSolver.totalTransfers +
                " | Step: " + dripSolver.transfersLastStep +
                " | Moved: " + dripSolver.movedThicknessLastStep.ToString("0.000");
        }

        string fluidFilmLine = "Fluid Film: -";

        if (fluidFilmSolver != null)
        {
            fluidFilmLine =
                "Fluid Film: " + (fluidFilmSolver.enableFluidFilm ? "ON" : "OFF") +
                " | Active: " + fluidFilmSolver.activeFluidCells +
                " | Moved: " + fluidFilmSolver.movedMassLastStep.ToString("0.000") +
                " | Speed: " + fluidFilmSolver.averageFilmSpeed.ToString("0.000");
        }

        string ropeLine = "Rope: -";

        if (ropeController != null)
        {
            ropeLine =
                "Rope: " + ropeController.ropeType +
                " | Mode: " + ropeController.visualMode +
                " | Tension: " + ropeController.tension.ToString("0.00") +
                " | Slack: " + (ropeController.slack01 * 100f).ToString("0") + "%" +
                " | Segments: " + ropeController.segmentCount;
        }

        outputText.text =
            "Simulation Stats\n" +
            "FPS: " + displayedFps.ToString("0") + "\n" +
            "Particles: " + activeParticles + " | Visible: " + visibleDroplets + "\n" +
            "Paint: " + remainingPaint.ToString("0.00") + " / " + paintPercent.ToString("0") + "%\n" +
            "Surface: " + surfaceName + " | Nozzle: " + nozzleShape + "\n" +
            impactLine + "\n" +
            surfaceLine + "\n" +
            dripLine + "\n" +
            fluidFilmLine + "\n" +
            ropeLine;
    }
}
