using TMPro;
using UnityEngine;

public class SimulationStatsUI : MonoBehaviour
{
    [Header("References")]
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;
    public TextMeshProUGUI outputText;

    [Header("Update Settings")]
    public float updateInterval = 0.25f;

    private float updateTimer;
    private float fpsTimer;
    private int frameCounter;
    private float displayedFps;

    void Awake()
    {
        if (paintEmitter == null)
        {
            paintEmitter = UnityEngine.Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (particleSimulator == null)
        {
            particleSimulator = UnityEngine.Object.FindFirstObjectByType<PaintParticleSimulator>();
        }

        if (canvasPainter == null)
        {
            canvasPainter = UnityEngine.Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (outputText == null)
        {
            outputText = GetComponent<TextMeshProUGUI>();
        }
    }

    void Update()
    {
        UpdateFpsCounter();

        updateTimer += Time.unscaledDeltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
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

        if (canvasPainter != null)
        {
            surfaceName = canvasPainter.surfaceType.ToString();
        }

        outputText.text =
            "Simulation Stats\n" +
            "FPS: " + displayedFps.ToString("0") + "\n" +
            "Particles: " + activeParticles + "\n" +
            "Visible Droplets: " + visibleDroplets + "\n" +
            "Paint: " + remainingPaint.ToString("0.00") + " / " + paintPercent.ToString("0") + "%\n" +
            "Surface: " + surfaceName + "\n" +
            "Nozzle: " + nozzleShape;
    }
}