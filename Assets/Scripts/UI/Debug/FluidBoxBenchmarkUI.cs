using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FluidBoxBenchmarkUI : MonoBehaviour
{
    public FluidBoxBenchmarkController benchmarkController;
    public GpuSphDebugStats stats;
    public FluidBoxMotionController motionController;
    public TextMeshProUGUI statsText;
    public bool autoCreateUi = true;
    public float refreshInterval = 0.15f;

    private readonly StringBuilder builder = new StringBuilder(512);
    private float refreshTimer;

    private void Awake()
    {
        ResolveReferences();
        if (autoCreateUi && statsText == null)
        {
            CreateRuntimeUi();
        }
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
        {
            return;
        }

        refreshTimer = 0f;
        RefreshText();
    }

    public void RefreshText()
    {
        ResolveReferences();
        if (statsText == null || stats == null)
        {
            return;
        }

        int rendered = stats.renderedParticleCount;
        builder.Clear();
        builder.AppendLine("Fluid Box GPU SPH Benchmark");
        builder.Append("Mode: ").AppendLine(stats.activeMode);
        builder.Append("Simulated particles: ").AppendLine(stats.simulatedParticleCount.ToString("N0"));
        builder.Append("Rendered particles: ").AppendLine(rendered.ToString("N0"));
        builder.Append("Render stride: ").AppendLine(stats.renderStride.ToString());
        builder.Append("GPU buffers: ").Append(stats.estimatedGpuMemoryMb.ToString("0.0")).AppendLine(" MB");
        builder.Append("Grid: ").Append(stats.gridDimensions.x).Append(" x ").Append(stats.gridDimensions.y).Append(" x ").AppendLine(stats.gridDimensions.z.ToString());
        builder.Append("Bounds: ").Append(stats.boundsSize.x.ToString("0.0")).Append(" x ").Append(stats.boundsSize.y.ToString("0.0")).Append(" x ").AppendLine(stats.boundsSize.z.ToString("0.0"));
        builder.Append("Substeps: ").AppendLine(stats.substeps.ToString());
        builder.Append("Smoothing length: ").AppendLine(stats.smoothingLength.ToString("0.000"));
        builder.Append("Rest density: ").AppendLine(stats.restDensity.ToString("0"));
        builder.Append("Viscosity: ").AppendLine(stats.viscosity.ToString("0.000"));
        builder.Append("FPS: ").AppendLine(stats.fps.ToString("0.0"));

        if (motionController != null)
        {
            builder.Append("Box motion: ").Append(motionController.motionMode).Append(motionController.motionEnabled ? " (on)" : " (off)").AppendLine();
        }

        if (stats.simulatedParticleCount >= 1000000)
        {
            builder.AppendLine("Warning: 1M mode is hardware-heavy; rendered count is intentionally reduced by stride.");
        }

        statsText.text = builder.ToString();
    }

    public void ResetFluid()
    {
        benchmarkController?.ResetFluid();
    }

    public void ApplyDebugMode()
    {
        benchmarkController?.ApplyDebugMode();
    }

    public void ApplyPresentationMode()
    {
        benchmarkController?.ApplyPresentationMode();
    }

    public void ApplyProfessorBenchmarkMode()
    {
        benchmarkController?.ApplyProfessorBenchmarkMode();
    }

    public void ApplyVRSafeMode()
    {
        benchmarkController?.ApplyVRSafeMode();
    }

    public void ToggleBoxMotion()
    {
        benchmarkController?.ToggleBoxMotion();
    }

    public void CycleBoxMotionMode()
    {
        benchmarkController?.CycleBoxMotionMode();
    }

    private void ResolveReferences()
    {
        if (benchmarkController == null)
        {
            benchmarkController = Object.FindFirstObjectByType<FluidBoxBenchmarkController>();
        }

        if (stats == null)
        {
            stats = Object.FindFirstObjectByType<GpuSphDebugStats>();
        }

        if (motionController == null)
        {
            motionController = Object.FindFirstObjectByType<FluidBoxMotionController>();
        }
    }

    private void CreateRuntimeUi()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("BenchmarkCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject panel = new GameObject("StatsPanel");
        panel.transform.SetParent(canvas.transform, false);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.58f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -16f);
        panelRect.sizeDelta = new Vector2(440f, 420f);

        GameObject textObject = new GameObject("StatsText");
        textObject.transform.SetParent(panel.transform, false);
        statsText = textObject.AddComponent<TextMeshProUGUI>();
        statsText.fontSize = 18f;
        statsText.color = Color.white;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform textRect = statsText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 12f);
        textRect.offsetMax = new Vector2(-14f, -12f);

        CreateButton(canvas.transform, "Reset Fluid", new Vector2(16f, -456f), ResetFluid);
        CreateButton(canvas.transform, "Debug", new Vector2(16f, -500f), ApplyDebugMode);
        CreateButton(canvas.transform, "Presentation", new Vector2(126f, -500f), ApplyPresentationMode);
        CreateButton(canvas.transform, "Professor 1M", new Vector2(270f, -500f), ApplyProfessorBenchmarkMode);
        CreateButton(canvas.transform, "VR Safe", new Vector2(16f, -544f), ApplyVRSafeMode);
        CreateButton(canvas.transform, "Toggle Motion", new Vector2(126f, -544f), ToggleBoxMotion);
        CreateButton(canvas.transform, "Cycle Motion", new Vector2(270f, -544f), CycleBoxMotionMode);
    }

    private void CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.22f, 0.28f, 0.88f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(128f, 34f);

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI labelText = textObject.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 14f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }
}
