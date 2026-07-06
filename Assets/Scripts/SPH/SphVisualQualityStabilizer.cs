using UnityEngine;

[DefaultExecutionOrder(20)]
public class SphVisualQualityStabilizer : MonoBehaviour
{
    private const string RuntimeObjectName = "SphVisualQualityStabilizer_Phase04B";

    [Header("Runtime Control")]
    public bool enforceHeroVisualDefaults = true;
    public bool reduceLegacyParticleNoise = true;
    public bool reduceCanvasSpeckle = true;
    public float enforcementDuration = 8f;

    [Header("Look")]
    public Color heroPaintColor = new Color(0.55f, 0.006f, 0.002f, 0.96f);

    private PaintEmitter paintEmitter;
    private PaintParticleSimulator particleSimulator;
    private CanvasPainter canvasPainter;
    private RealGpuSphController gpuSphController;
    private float elapsed;
    private float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphVisualQualityStabilizer>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphVisualQualityStabilizer>();
    }

    private void Start()
    {
        FindReferences();
        ApplyOnce();
    }

    private void Update()
    {
        if (!enforceHeroVisualDefaults)
        {
            return;
        }

        elapsed += Time.deltaTime;
        timer -= Time.deltaTime;
        if (timer > 0f && elapsed > enforcementDuration)
        {
            return;
        }

        if (timer <= 0f)
        {
            timer = 0.5f;
            FindReferences();
            ApplyOnce();
        }
    }

    private void FindReferences()
    {
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (particleSimulator == null) particleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (gpuSphController == null) gpuSphController = Object.FindFirstObjectByType<RealGpuSphController>();
    }

    private void ApplyOnce()
    {
        if (gpuSphController != null)
        {
            gpuSphController.paintColor = heroPaintColor;
            gpuSphController.drawGpuParticles = false;
            gpuSphController.viscosityDrag = Mathf.Max(gpuSphController.viscosityDrag, 4.0f);
            gpuSphController.cohesionStrength = Mathf.Max(gpuSphController.cohesionStrength, 4.2f);
            gpuSphController.spillPush = Mathf.Min(gpuSphController.spillPush, 1.35f);
            gpuSphController.maxParticleVelocity = Mathf.Min(gpuSphController.maxParticleVelocity, 8.0f);
            gpuSphController.containedParticleRenderScale = 0.0f;
            gpuSphController.airborneParticleRenderScale = Mathf.Min(gpuSphController.airborneParticleRenderScale, 0.85f);
            gpuSphController.depositedParticleRenderScale = Mathf.Min(gpuSphController.depositedParticleRenderScale, 0.08f);
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = heroPaintColor;
            paintEmitter.viscosity = Mathf.Max(paintEmitter.viscosity, 1.75f);
            paintEmitter.downwardStartSpeed = Mathf.Clamp(paintEmitter.downwardStartSpeed, 1.10f, 1.55f);
            paintEmitter.nozzleVelocityInfluence = Mathf.Min(paintEmitter.nozzleVelocityInfluence, 0.45f);

            if (reduceLegacyParticleNoise)
            {
                paintEmitter.particlesPerUnitFlow = Mathf.Min(paintEmitter.particlesPerUnitFlow, 34f);
                paintEmitter.randomSpread = Mathf.Min(paintEmitter.randomSpread, 0.045f);
                paintEmitter.flowNoiseAmount = Mathf.Min(paintEmitter.flowNoiseAmount, 0.08f);
                paintEmitter.sloshFlowInfluence = Mathf.Min(paintEmitter.sloshFlowInfluence, 0.28f);
                paintEmitter.sloshDirectionInfluence = Mathf.Min(paintEmitter.sloshDirectionInfluence, 0.28f);
                paintEmitter.minParticleRadius = Mathf.Max(paintEmitter.minParticleRadius, 0.026f);
                paintEmitter.maxParticleRadius = Mathf.Max(paintEmitter.maxParticleRadius, 0.072f);
                paintEmitter.maxParticlesEmittedPerFrame = Mathf.Min(paintEmitter.maxParticlesEmittedPerFrame, 64);
                paintEmitter.maxEmissionBacklog = Mathf.Min(paintEmitter.maxEmissionBacklog, 32f);
            }
        }

        if (particleSimulator != null && reduceLegacyParticleNoise)
        {
            particleSimulator.maxParticles = Mathf.Min(particleSimulator.maxParticles, 700);
            particleSimulator.maxParticlesSimulatedPerFrame = Mathf.Min(particleSimulator.maxParticlesSimulatedPerFrame, 700);
            particleSimulator.visualPoolSize = Mathf.Min(particleSimulator.visualPoolSize, 90);
            particleSimulator.maxInteractionChecks = Mathf.Min(particleSimulator.maxInteractionChecks, 10);
            particleSimulator.cohesionStrength = Mathf.Max(particleSimulator.cohesionStrength, 0.24f);
            particleSimulator.separationStrength = Mathf.Min(particleSimulator.separationStrength, 0.045f);
            particleSimulator.windStrength = Mathf.Min(particleSimulator.windStrength, 0.04f);
            particleSimulator.turbulenceStrength = Mathf.Min(particleSimulator.turbulenceStrength, 0.06f);
            particleSimulator.SetMaxParticlesSafely(particleSimulator.maxParticles);
        }

        if (canvasPainter != null && reduceCanvasSpeckle)
        {
            canvasPainter.paintOpacity = Mathf.Clamp(canvasPainter.paintOpacity, 0.82f, 0.95f);
            canvasPainter.sprayAmount = Mathf.Min(canvasPainter.sprayAmount, 0.20f);
            canvasPainter.smearLength = Mathf.Max(canvasPainter.smearLength, 0.95f);
            canvasPainter.smearSteps = Mathf.Max(canvasPainter.smearSteps, 5);
            canvasPainter.edgeIrregularity = Mathf.Min(canvasPainter.edgeIrregularity, 0.32f);
        }
    }
}
