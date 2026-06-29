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
    public Vector2 anchoredPosition = new Vector2(14f, -140f);

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
        if (readout == null)
        {
            return;
        }

        builder.Length = 0;
        builder.AppendLine("Bucket SPH Phase 4");

        if (controller != null)
        {
            builder.Append("Mode: ");
            builder.AppendLine(controller.CurrentMode.ToString());
            builder.Append("Profile: ");
            builder.AppendLine(controller.ActiveProfile != null ? controller.ActiveProfile.name : "Runtime");
        }

        if (stats != null)
        {
            builder.Append("Particles: ");
            builder.Append(stats.simulatedParticleCount);
            builder.Append(" / rendered ");
            builder.AppendLine(stats.renderedParticleCount.ToString());
            builder.Append("GPU MB: ");
            builder.AppendLine(stats.estimatedTotalGpuMemoryMb.ToString("0.0"));
            builder.Append("Grid: ");
            builder.Append(stats.gridDimensions);
            builder.Append(" overflow ");
            builder.AppendLine(stats.gridOverflowCount.ToString());

            if (stats.gridOverflowCount > 0)
            {
                builder.AppendLine("WARNING: SPH grid overflow detected. Increase grid capacity, reduce particle density, or use a higher render stride/profile budget.");
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
        }

        if (motionDataProvider != null)
        {
            builder.Append("Bucket v: ");
            builder.AppendLine(motionDataProvider.WorldVelocity.magnitude.ToString("0.00"));
        }

        readout.text = builder.ToString();
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
        rect.sizeDelta = new Vector2(360f, 190f);
    }
}
