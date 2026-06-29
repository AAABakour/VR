using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class BucketSphDebugUI : MonoBehaviour
{
    public BucketSphFluidController controller;
    public GpuSphDebugStats stats;
    public BucketSphNozzleEmitter nozzleEmitter;
    public BucketMotionDataProvider motionDataProvider;
    public bool autoCreateUi = true;
    public bool enableKeyboardShortcuts = true;
    public Vector2 anchoredPosition = new Vector2(14f, -14f);

    private Text readout;
    private readonly StringBuilder builder = new StringBuilder(512);

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
        builder.AppendLine("Bucket SPH Phase 4.3");

        if (controller != null)
        {
            builder.Append("Mode: ");
            builder.AppendLine(controller.CurrentMode.ToString());
            builder.Append("Profile: ");
            builder.AppendLine(controller.ActiveProfile != null ? controller.ActiveProfile.name : "Runtime");

            if (controller.CurrentMode == BucketSphMode.InternalAndEmission && !controller.internalAndEmissionSupported)
            {
                builder.AppendLine("Internal + Emission: postponed");
            }
        }

        if (stats != null)
        {
            builder.Append("Simulated: ");
            builder.Append(stats.simulatedParticleCount);
            builder.Append("  Active: ");
            builder.Append(stats.activeParticleCount);
            builder.Append("  Inactive: ");
            builder.AppendLine(stats.inactiveParticleCount.ToString());
            builder.Append("Rendered slots: ");
            builder.AppendLine(stats.renderedParticleCount.ToString());
            builder.Append("Total emitted: ");
            builder.AppendLine(stats.totalEmittedParticleCount.ToString());
            builder.Append("Render stride: ");
            builder.AppendLine(stats.renderStride.ToString());
            builder.Append("Domain: ");
            builder.Append(stats.simulationDomain);
            builder.Append("  Frame: ");
            builder.AppendLine(stats.simulationFrame);
            builder.Append("GPU MB: ");
            builder.AppendLine(stats.estimatedTotalGpuMemoryMb.ToString("0.0"));
            builder.Append("Grid: ");
            builder.AppendLine(stats.gridDimensions.ToString());
            builder.Append("Max/cell: ");
            builder.Append(stats.maxParticlesPerCell);
            builder.Append("  Overflow: ");
            builder.AppendLine(stats.gridOverflowCount.ToString());
            builder.Append("Solver initialized: ");
            builder.Append(stats.solverInitialized ? "Yes" : "No");
            builder.Append("  Buffers: ");
            builder.AppendLine(stats.buffersValid ? "Valid" : "Invalid");
            builder.Append("Lifetime: ");
            builder.Append(stats.emitterLifetime.ToString("0.0"));
            builder.AppendLine("s");

            if (stats.gridOverflowCount > 0)
            {
                builder.AppendLine("WARNING: SPH grid overflow detected.");
            }
        }

        if (nozzleEmitter != null)
        {
            builder.Append("Emitter: ");
            builder.Append(nozzleEmitter.emitOnUpdate ? "On" : "Off");
            builder.Append(" @ ");
            builder.Append(nozzleEmitter.particlesPerSecond.ToString("0"));
            builder.AppendLine("/s");
            builder.Append("Last burst: ");
            builder.AppendLine(nozzleEmitter.EmittedThisFrame.ToString());
            builder.Append("Particle lifetime: ");
            builder.Append(nozzleEmitter.particleLifetime.ToString("0.0"));
            builder.AppendLine("s");
            builder.Append("Nozzle world: ");
            builder.AppendLine(nozzleEmitter.NozzleWorldPosition.ToString("F2"));
        }

        if (motionDataProvider != null)
        {
            builder.Append("Bucket v: ");
            builder.AppendLine(motionDataProvider.WorldVelocity.magnitude.ToString("0.00"));
        }

        builder.AppendLine("Controls: R Reset | 1 Internal | 2 Nozzle | 3 Mixed future | E Emit");
        builder.AppendLine("Profiles: F1 Bucket Demo | F2 Presentation | F3 Professor 1M | F4 VR Safe");

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
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject textObject = new GameObject("BucketSphDebugReadout");
        textObject.transform.SetParent(canvas.transform, false);
        readout = textObject.AddComponent<Text>();
        readout.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        readout.fontSize = 14;
        readout.color = new Color(0.86f, 0.95f, 1f, 0.95f);
        readout.alignment = TextAnchor.UpperLeft;
        readout.raycastTarget = false;

        RectTransform rect = readout.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(560f, 430f);
    }
}
