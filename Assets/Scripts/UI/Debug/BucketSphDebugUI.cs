using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class BucketSphDebugUI : MonoBehaviour
{
    public BucketSphFluidController controller;
    public GpuSphDebugStats stats;
    public BucketSphNozzleEmitter nozzleEmitter;
    public BucketPaintReservoir reservoir;
    public BucketMotionDataProvider motionDataProvider;
    public bool autoCreateUi = true;
    public bool enableKeyboardShortcuts = true;
    public Vector2 anchoredPosition = new Vector2(14f, -14f);

    private Text readout;
    private Image backgroundPanel;
    private readonly StringBuilder builder = new StringBuilder(1600);

    private void Awake()
    {
        ResolveReferences();
        if (autoCreateUi)
        {
            EnsureRuntimeUi();
        }
    }

    private void Update()
    {
        HandleKeyboardShortcuts();

        if (readout == null)
        {
            return;
        }

        builder.Length = 0;
        builder.AppendLine("BUCKET SPH PHASE 4.5");
        builder.AppendLine("A - Mode");

        if (controller != null)
        {
            builder.Append("Mode: ");
            builder.AppendLine(controller.CurrentMode.ToString());
            builder.Append("Profile: ");
            builder.AppendLine(controller.ActiveProfile != null ? controller.ActiveProfile.name : "Runtime");
            builder.Append("Profile requested particles: ");
            builder.AppendLine(controller.ActiveProfile != null ? controller.ActiveProfile.targetParticleCount.ToString("N0") : "Unknown");
            builder.Append("Emission enabled: ");
            builder.AppendLine(nozzleEmitter != null && nozzleEmitter.emitOnUpdate ? "Yes" : "No");

            if (controller.CurrentMode == BucketSphMode.InternalAndEmission && !controller.internalAndEmissionSupported)
            {
                builder.AppendLine("Internal + Emission: postponed");
            }
        }

        if (stats != null)
        {
            builder.Append("Domain: ");
            builder.Append(stats.simulationDomain);
            builder.Append("  Frame: ");
            builder.AppendLine(stats.simulationFrame);

            builder.AppendLine();
            builder.AppendLine("B - Particle Verification");
            builder.Append("Solver requested: ");
            builder.AppendLine(stats.requestedParticleCount.ToString("N0"));
            builder.Append("Solver allocated: ");
            builder.AppendLine(stats.allocatedParticleCount.ToString("N0"));
            builder.Append("GPU buffer capacity: ");
            builder.AppendLine(stats.gpuBufferParticleCapacity.ToString("N0"));
            builder.Append("Active particles: ");
            builder.AppendLine(stats.activeParticleCount.ToString("N0"));
            builder.Append("Inactive particles: ");
            builder.AppendLine(stats.inactiveParticleCount.ToString("N0"));
            builder.Append("Total emitted: ");
            builder.AppendLine(stats.totalEmittedParticleCount.ToString("N0"));
            builder.Append("Rendered capacity: ");
            builder.AppendLine(stats.allocatedRenderedCapacity.ToString("N0"));
            builder.Append("Render stride: ");
            builder.AppendLine(stats.renderStride.ToString("N0"));
            builder.Append("Particle stride bytes: ");
            builder.AppendLine(stats.particleStrideBytes.ToString());
            builder.Append("Buffers valid: ");
            builder.Append(stats.buffersValid ? "Yes" : "No");
            builder.Append("  Solver initialized: ");
            builder.AppendLine(stats.solverInitialized ? "Yes" : "No");

            builder.AppendLine();
            builder.AppendLine("C - Professor 1M Verification");
            if (stats.isProfessorBenchmarkActive)
            {
                builder.AppendLine("PROFESSOR 1M MODE ACTIVE");
                builder.Append("Allocated simulated particles: ");
                builder.AppendLine(stats.allocatedParticleCount.ToString("N0"));
                builder.Append("Rendered particles at current stride: ");
                builder.AppendLine(stats.expectedRenderedParticleCount.ToString("N0"));
            }
            else
            {
                builder.AppendLine("Current profile is not 1M benchmark");
                builder.AppendLine("Press F3 for Professor 1M");
            }
        }

        builder.AppendLine();
        builder.AppendLine("D - Reservoir");
        if (nozzleEmitter != null)
        {
            builder.Append("Emitter: ");
            builder.Append(nozzleEmitter.emitOnUpdate ? "On" : "Off");
            builder.Append(" @ ");
            builder.Append(nozzleEmitter.particlesPerSecond.ToString("0"));
            builder.Append("/s  Effective: ");
            builder.Append(nozzleEmitter.EffectiveEmissionRate.ToString("0"));
            builder.AppendLine("/s");
            builder.Append("Requested/frame: ");
            builder.Append(nozzleEmitter.RequestedThisFrame);
            builder.Append("  Actual/frame: ");
            builder.AppendLine(nozzleEmitter.ActualEmittedThisFrame.ToString());
            builder.Append("Flow factor: ");
            builder.AppendLine(nozzleEmitter.LastFlowFactor.ToString("0.00"));
            builder.Append("Last consumed: ");
            builder.AppendLine(nozzleEmitter.LastConsumedPaintAmount.ToString("0.0000"));
            builder.Append("Particle lifetime: ");
            builder.Append(nozzleEmitter.particleLifetime.ToString("0.0"));
            builder.AppendLine("s");
            builder.Append("Nozzle world: ");
            builder.AppendLine(nozzleEmitter.NozzleWorldPosition.ToString("F2"));

            if (nozzleEmitter.IsEmissionBlockedByEmptyReservoir)
            {
                builder.AppendLine("Emission blocked: Reservoir Empty");
            }
        }

        if (reservoir != null)
        {
            builder.Append("Paint remaining: ");
            builder.Append(reservoir.RemainingPaintAmount.ToString("0.000"));
            builder.Append(" / ");
            builder.Append(reservoir.maxPaintAmount.ToString("0.000"));
            builder.Append("  Fill: ");
            builder.Append((reservoir.FillPercent * 100f).ToString("0"));
            builder.AppendLine("%");
            builder.Append("Reservoir: ");
            builder.AppendLine(GetReservoirStatus());
            builder.Append("Estimated seconds remaining: ");
            builder.AppendLine(FormatSeconds(reservoir.estimatedSecondsRemaining));
        }

        if (stats != null)
        {
            builder.AppendLine();
            builder.AppendLine("E - SPH Health");
            builder.Append("GPU memory MB: ");
            builder.AppendLine(stats.estimatedTotalGpuMemoryMb.ToString("0.0"));
            builder.Append("Grid: ");
            builder.AppendLine(stats.gridDimensions.ToString());
            builder.Append("Max particles/cell: ");
            builder.Append(stats.maxParticlesPerCell);
            builder.Append("  Overflow: ");
            builder.AppendLine(stats.gridOverflowCount.ToString());
            builder.Append("FPS: ");
            builder.AppendLine(stats.fps.ToString("0"));
            builder.Append("Substeps: ");
            builder.Append(stats.substeps);
            builder.Append("  Timestep: ");
            builder.AppendLine(stats.timestep.ToString("0.0000"));
            builder.Append("Smoothing: ");
            builder.Append(stats.smoothingLength.ToString("0.000"));
            builder.Append("  Viscosity: ");
            builder.AppendLine(stats.viscosity.ToString("0.000"));

            if (stats.gridOverflowCount > 0)
            {
                builder.AppendLine("WARNING: SPH grid overflow detected.");
            }
        }

        if (motionDataProvider != null)
        {
            builder.Append("Bucket v: ");
            builder.AppendLine(motionDataProvider.WorldVelocity.magnitude.ToString("0.00"));
        }

        builder.AppendLine();
        builder.AppendLine("F - Controls");
        builder.AppendLine("R Reset/refill | E Toggle emission | P Add paint");
        builder.AppendLine("1 Internal | 2 Nozzle | F1 Demo | F2 Presentation");
        builder.AppendLine("F3 Professor 1M | F4 VR Safe | I DEBUG infinite");

        readout.text = builder.ToString();
    }

    private void HandleKeyboardShortcuts()
    {
        if (!enableKeyboardShortcuts)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetFluid();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetInternalFluidMode();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetNozzleEmissionMode();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetInternalAndEmissionMode();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ToggleEmission();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            AddPaint();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInfiniteDebugEmission();
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            ApplyBucketNozzleDemoProfile();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            ApplyPresentationProfile();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            ApplyProfessorBenchmarkProfile();
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            ApplyVrSafeProfile();
        }
    }

    [ContextMenu("Resolve Debug UI References")]
    public void ResolveReferences()
    {
        if (controller == null)
        {
            controller = Object.FindFirstObjectByType<BucketSphFluidController>();
        }

        if (stats == null)
        {
            stats = Object.FindFirstObjectByType<GpuSphDebugStats>();
        }

        if (nozzleEmitter == null)
        {
            nozzleEmitter = Object.FindFirstObjectByType<BucketSphNozzleEmitter>();
        }

        if (reservoir == null)
        {
            reservoir = Object.FindFirstObjectByType<BucketPaintReservoir>();
        }

        if (motionDataProvider == null)
        {
            motionDataProvider = Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }
    }

    public void CycleMode()
    {
        if (controller != null)
        {
            controller.CycleMode();
        }
    }

    public void ResetFluid()
    {
        if (controller != null)
        {
            controller.ResetBucketSph();
        }
    }

    public void SetInternalFluidMode()
    {
        if (controller != null)
        {
            controller.SetInternalFluidMode();
        }
    }

    public void SetNozzleEmissionMode()
    {
        if (controller != null)
        {
            controller.SetNozzleEmissionMode();
        }
    }

    public void SetInternalAndEmissionMode()
    {
        if (controller != null)
        {
            controller.SetInternalAndEmissionMode();
        }
    }

    public void SetDebugStaticEmissionMode()
    {
        if (controller != null)
        {
            controller.SetDebugStaticEmissionMode();
        }
    }

    public void ToggleEmission()
    {
        if (controller != null)
        {
            controller.ToggleEmission();
        }
    }

    public void AddPaint()
    {
        if (controller != null)
        {
            controller.AddPaint(0.5f);
        }
    }

    public void ToggleInfiniteDebugEmission()
    {
        if (controller != null)
        {
            controller.ToggleInfiniteDebugEmission();
        }
    }

    public void ApplyDebugProfile()
    {
        if (controller != null)
        {
            controller.ApplyDebugProfile();
        }
    }

    public void ApplyBucketNozzleDemoProfile()
    {
        if (controller != null)
        {
            controller.ApplyBucketNozzleDemoProfile();
        }
    }

    public void ApplyPresentationProfile()
    {
        if (controller != null)
        {
            controller.ApplyPresentationProfile();
        }
    }

    public void ApplyProfessorBenchmarkProfile()
    {
        if (controller != null)
        {
            controller.ApplyProfessorBenchmarkProfile();
        }
    }

    public void ApplyVrSafeProfile()
    {
        if (controller != null)
        {
            controller.ApplyVrSafeProfile();
        }
    }

    private void EnsureRuntimeUi()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("BucketSphDebugCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 5000);
        }

        GameObject panelObject = new GameObject("BucketSphDebugPanel");
        panelObject.transform.SetParent(canvas.transform, false);
        backgroundPanel = panelObject.AddComponent<Image>();
        backgroundPanel.color = new Color(0.02f, 0.025f, 0.03f, 0.82f);
        backgroundPanel.raycastTarget = false;

        RectTransform panelRect = backgroundPanel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = new Vector2(690f, 900f);

        GameObject textObject = new GameObject("BucketSphDebugReadout");
        textObject.transform.SetParent(panelObject.transform, false);
        readout = textObject.AddComponent<Text>();
        readout.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        readout.fontSize = 16;
        readout.color = new Color(0.94f, 0.97f, 1f, 1f);
        readout.alignment = TextAnchor.UpperLeft;
        readout.raycastTarget = false;
        readout.horizontalOverflow = HorizontalWrapMode.Wrap;
        readout.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = readout.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(16f, 14f);
        rect.offsetMax = new Vector2(-16f, -14f);
    }

    private string GetReservoirStatus()
    {
        if (reservoir == null)
        {
            return "MISSING";
        }

        if (reservoir.allowInfiniteDebugEmission)
        {
            return "INFINITE DEBUG";
        }

        if (reservoir.IsEmpty)
        {
            return "EMPTY";
        }

        if (reservoir.FillPercent >= 0.98f)
        {
            return "FULL";
        }

        if (reservoir.FillPercent <= 0.18f)
        {
            return "LOW";
        }

        return "DRAINING";
    }

    private static string FormatSeconds(float seconds)
    {
        if (float.IsPositiveInfinity(seconds))
        {
            return "Infinite debug";
        }

        if (seconds <= 0f)
        {
            return "0.0s";
        }

        return seconds.ToString("0.0") + "s";
    }
}
