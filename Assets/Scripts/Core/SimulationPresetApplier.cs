using UnityEngine;

public class SimulationPresetApplier : MonoBehaviour
{
    public enum SimulationPreset
    {
        CleanSpiral,
        SplashyPaint,
        DenseFlow,
        DoubleHolePattern,
        MetalSpread
    }

    [Header("References")]
    public PendulumController pendulumController;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;

    [Header("Current Preset")]
    public SimulationPreset currentPreset = SimulationPreset.CleanSpiral;

    [Header("Impact V2 References")]
    public PaintImpactEngineV2 impactEngine;

    [Header("Impact V2 Surface Profiles")]
    public PaintSurfaceProfile paperProfile;
    public PaintSurfaceProfile canvasProfile;
    public PaintSurfaceProfile woodProfile;
    public PaintSurfaceProfile metalProfile;

    void Awake()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (pendulumController == null)
        {
            pendulumController = Object.FindFirstObjectByType<PendulumController>();
        }

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
    }

    public void ApplyCurrentPreset()
    {
        ApplyPreset(currentPreset);
    }

    public void ApplyPresetByIndex(int presetIndex)
    {
        currentPreset = (SimulationPreset)presetIndex;
        ApplyPreset(currentPreset);
    }

    public void ApplyCleanSpiral()
    {
        ApplyPreset(SimulationPreset.CleanSpiral);
    }

    public void ApplySplashyPaint()
    {
        ApplyPreset(SimulationPreset.SplashyPaint);
    }

    public void ApplyDenseFlow()
    {
        ApplyPreset(SimulationPreset.DenseFlow);
    }

    public void ApplyDoubleHolePattern()
    {
        ApplyPreset(SimulationPreset.DoubleHolePattern);
    }

    public void ApplyMetalSpread()
    {
        ApplyPreset(SimulationPreset.MetalSpread);
    }

    private void ApplyPreset(SimulationPreset preset)
    {
        AutoFindReferences();

        switch (preset)
        {
            case SimulationPreset.CleanSpiral:
                ApplyCleanSpiralPreset();
                break;

            case SimulationPreset.SplashyPaint:
                ApplySplashyPaintPreset();
                break;

            case SimulationPreset.DenseFlow:
                ApplyDenseFlowPreset();
                break;

            case SimulationPreset.DoubleHolePattern:
                ApplyDoubleHolePatternPreset();
                break;

            case SimulationPreset.MetalSpread:
                ApplyMetalSpreadPreset();
                break;
        }

        ResetSimulationAfterPreset();
    }

    private void ApplyCleanSpiralPreset()
    {
        if (pendulumController != null)
        {
            pendulumController.ropeLength = 2.2f;
            pendulumController.startAngleDegrees = 35f;
            pendulumController.initialAngularVelocity = 0f;
            pendulumController.damping = 0.05f;
            pendulumController.enableDepthSwing = true;
            pendulumController.startAngleZDegrees = 18f;
            pendulumController.initialAngularVelocityZ = 0.8f;
            pendulumController.zSwingStrength = 0.6f;

            pendulumController.ropeElasticity = 0.025f;
            pendulumController.wobbleStrength = 5f;
            pendulumController.torsionStrength = 10f;
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = Color.red;
            paintEmitter.initialPaintAmount = 5f;
            paintEmitter.holeDiameter = 0.04f;
            paintEmitter.viscosity = 1.2f;
            paintEmitter.nozzleShape = PaintEmitter.NozzleShape.Circular;

            paintEmitter.baseFlowRate = 0.22f;
            paintEmitter.particlesPerUnitFlow = 55f;
            paintEmitter.randomSpread = 0.08f;

            paintEmitter.enableInternalSlosh = true;
            paintEmitter.sloshFlowInfluence = 0.35f;
            paintEmitter.flowNoiseAmount = 0.12f;
        }

        if (particleSimulator != null)
        {
            particleSimulator.maxParticles = 900;
            particleSimulator.interactionRadius = 0.14f;
            particleSimulator.cohesionStrength = 0.10f;
            particleSimulator.separationStrength = 0.05f;
            particleSimulator.maxInteractionChecks = 12;

            particleSimulator.windStrength = 0.06f;
            particleSimulator.turbulenceStrength = 0.10f;
            particleSimulator.visualPoolSize = 100;
        }

        if (canvasPainter != null)
        {
            canvasPainter.surfaceType = CanvasPainter.CanvasSurfaceType.Canvas;
            canvasPainter.paintOpacity = 0.85f;
            canvasPainter.sprayAmount = 0.25f;
            canvasPainter.smearLength = 0.8f;
            canvasPainter.smearSteps = 4;
            canvasPainter.edgeIrregularity = 0.45f;
        }
        ApplyImpactSurfaceProfile(canvasProfile);
    }

    private void ApplySplashyPaintPreset()
    {
        if (pendulumController != null)
        {
            pendulumController.ropeLength = 2.1f;
            pendulumController.startAngleDegrees = 42f;
            pendulumController.initialAngularVelocity = 0.5f;
            pendulumController.damping = 0.04f;
            pendulumController.enableDepthSwing = true;
            pendulumController.startAngleZDegrees = 25f;
            pendulumController.initialAngularVelocityZ = 1.1f;
            pendulumController.zSwingStrength = 0.75f;

            pendulumController.ropeElasticity = 0.045f;
            pendulumController.wobbleStrength = 10f;
            pendulumController.torsionStrength = 22f;
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = Color.red;
            paintEmitter.initialPaintAmount = 5f;
            paintEmitter.holeDiameter = 0.045f;
            paintEmitter.viscosity = 0.75f;
            paintEmitter.nozzleShape = PaintEmitter.NozzleShape.Irregular;

            paintEmitter.baseFlowRate = 0.30f;
            paintEmitter.particlesPerUnitFlow = 75f;
            paintEmitter.randomSpread = 0.20f;

            paintEmitter.enableInternalSlosh = true;
            paintEmitter.sloshFlowInfluence = 0.8f;
            paintEmitter.sloshDirectionInfluence = 0.75f;
            paintEmitter.flowNoiseAmount = 0.28f;
        }

        if (particleSimulator != null)
        {
            particleSimulator.maxParticles = 1200;
            particleSimulator.interactionRadius = 0.17f;
            particleSimulator.cohesionStrength = 0.12f;
            particleSimulator.separationStrength = 0.09f;
            particleSimulator.maxInteractionChecks = 16;

            particleSimulator.windStrength = 0.16f;
            particleSimulator.turbulenceStrength = 0.30f;
            particleSimulator.visualPoolSize = 120;
        }

        if (canvasPainter != null)
        {
            canvasPainter.surfaceType = CanvasPainter.CanvasSurfaceType.Canvas;
            canvasPainter.paintOpacity = 0.9f;
            canvasPainter.sprayAmount = 0.65f;
            canvasPainter.smearLength = 1.2f;
            canvasPainter.smearSteps = 5;
            canvasPainter.edgeIrregularity = 0.6f;
        }
        ApplyImpactSurfaceProfile(canvasProfile);
    }

    private void ApplyDenseFlowPreset()
    {
        if (pendulumController != null)
        {
            pendulumController.ropeLength = 2.25f;
            pendulumController.startAngleDegrees = 38f;
            pendulumController.initialAngularVelocity = 0.2f;
            pendulumController.damping = 0.055f;
            pendulumController.enableDepthSwing = true;
            pendulumController.startAngleZDegrees = 20f;
            pendulumController.initialAngularVelocityZ = 0.7f;
            pendulumController.zSwingStrength = 0.55f;

            pendulumController.ropeElasticity = 0.03f;
            pendulumController.wobbleStrength = 7f;
            pendulumController.torsionStrength = 15f;
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = Color.red;
            paintEmitter.initialPaintAmount = 6f;
            paintEmitter.holeDiameter = 0.05f;
            paintEmitter.viscosity = 1.4f;
            paintEmitter.nozzleShape = PaintEmitter.NozzleShape.Circular;

            paintEmitter.baseFlowRate = 0.35f;
            paintEmitter.particlesPerUnitFlow = 80f;
            paintEmitter.randomSpread = 0.10f;

            paintEmitter.enableInternalSlosh = true;
            paintEmitter.sloshFlowInfluence = 0.45f;
            paintEmitter.flowNoiseAmount = 0.16f;
        }

        if (particleSimulator != null)
        {
            particleSimulator.maxParticles = 1300;
            particleSimulator.interactionRadius = 0.18f;
            particleSimulator.cohesionStrength = 0.22f;
            particleSimulator.separationStrength = 0.08f;
            particleSimulator.maxInteractionChecks = 18;

            particleSimulator.windStrength = 0.08f;
            particleSimulator.turbulenceStrength = 0.14f;
            particleSimulator.visualPoolSize = 130;
        }

        if (canvasPainter != null)
        {
            canvasPainter.surfaceType = CanvasPainter.CanvasSurfaceType.Wood;
            canvasPainter.paintOpacity = 0.9f;
            canvasPainter.sprayAmount = 0.35f;
            canvasPainter.smearLength = 0.75f;
            canvasPainter.smearSteps = 4;
            canvasPainter.edgeIrregularity = 0.7f;
        }
        ApplyImpactSurfaceProfile(woodProfile);
    }

    private void ApplyDoubleHolePatternPreset()
    {
        if (pendulumController != null)
        {
            pendulumController.ropeLength = 2.2f;
            pendulumController.startAngleDegrees = 40f;
            pendulumController.initialAngularVelocity = 0.1f;
            pendulumController.damping = 0.05f;
            pendulumController.enableDepthSwing = true;
            pendulumController.startAngleZDegrees = 22f;
            pendulumController.initialAngularVelocityZ = 0.9f;
            pendulumController.zSwingStrength = 0.7f;

            pendulumController.ropeElasticity = 0.035f;
            pendulumController.wobbleStrength = 8f;
            pendulumController.torsionStrength = 18f;
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = Color.red;
            paintEmitter.initialPaintAmount = 5f;
            paintEmitter.holeDiameter = 0.04f;
            paintEmitter.viscosity = 1f;
            paintEmitter.nozzleShape = PaintEmitter.NozzleShape.DoubleHole;
            paintEmitter.doubleHoleSpacing = 0.08f;

            paintEmitter.baseFlowRate = 0.28f;
            paintEmitter.particlesPerUnitFlow = 70f;
            paintEmitter.randomSpread = 0.14f;

            paintEmitter.enableInternalSlosh = true;
            paintEmitter.sloshFlowInfluence = 0.65f;
            paintEmitter.flowNoiseAmount = 0.22f;
        }

        if (particleSimulator != null)
        {
            particleSimulator.maxParticles = 1200;
            particleSimulator.interactionRadius = 0.16f;
            particleSimulator.cohesionStrength = 0.14f;
            particleSimulator.separationStrength = 0.07f;
            particleSimulator.maxInteractionChecks = 16;

            particleSimulator.windStrength = 0.14f;
            particleSimulator.turbulenceStrength = 0.22f;
            particleSimulator.visualPoolSize = 120;
        }

        if (canvasPainter != null)
        {
            canvasPainter.surfaceType = CanvasPainter.CanvasSurfaceType.Canvas;
            canvasPainter.paintOpacity = 0.85f;
            canvasPainter.sprayAmount = 0.45f;
            canvasPainter.smearLength = 1.1f;
            canvasPainter.smearSteps = 5;
            canvasPainter.edgeIrregularity = 0.5f;
        }
        ApplyImpactSurfaceProfile(canvasProfile);
    }

    private void ApplyMetalSpreadPreset()
    {
        if (pendulumController != null)
        {
            pendulumController.ropeLength = 2.15f;
            pendulumController.startAngleDegrees = 36f;
            pendulumController.initialAngularVelocity = 0.4f;
            pendulumController.damping = 0.035f;
            pendulumController.enableDepthSwing = true;
            pendulumController.startAngleZDegrees = 24f;
            pendulumController.initialAngularVelocityZ = 1.0f;
            pendulumController.zSwingStrength = 0.75f;

            pendulumController.ropeElasticity = 0.04f;
            pendulumController.wobbleStrength = 9f;
            pendulumController.torsionStrength = 20f;
        }

        if (paintEmitter != null)
        {
            paintEmitter.paintColor = Color.red;
            paintEmitter.initialPaintAmount = 5f;
            paintEmitter.holeDiameter = 0.042f;
            paintEmitter.viscosity = 0.65f;
            paintEmitter.nozzleShape = PaintEmitter.NozzleShape.Slit;
            paintEmitter.slitWidth = 0.13f;

            paintEmitter.baseFlowRate = 0.32f;
            paintEmitter.particlesPerUnitFlow = 75f;
            paintEmitter.randomSpread = 0.18f;

            paintEmitter.enableInternalSlosh = true;
            paintEmitter.sloshFlowInfluence = 0.75f;
            paintEmitter.sloshDirectionInfluence = 0.7f;
            paintEmitter.flowNoiseAmount = 0.25f;
        }

        if (particleSimulator != null)
        {
            particleSimulator.maxParticles = 1200;
            particleSimulator.interactionRadius = 0.16f;
            particleSimulator.cohesionStrength = 0.10f;
            particleSimulator.separationStrength = 0.09f;
            particleSimulator.maxInteractionChecks = 16;

            particleSimulator.windStrength = 0.20f;
            particleSimulator.turbulenceStrength = 0.32f;
            particleSimulator.visualPoolSize = 120;
        }

        if (canvasPainter != null)
        {
            canvasPainter.surfaceType = CanvasPainter.CanvasSurfaceType.Metal;
            canvasPainter.paintOpacity = 0.95f;
            canvasPainter.sprayAmount = 0.7f;
            canvasPainter.smearLength = 1.8f;
            canvasPainter.smearSteps = 6;
            canvasPainter.edgeIrregularity = 0.35f;
        }
        ApplyImpactSurfaceProfile(metalProfile);
    }

    private void ApplyImpactSurfaceProfile(PaintSurfaceProfile profile)
    {
        if (impactEngine != null)
        {
            impactEngine.SetSurfaceProfile(profile);
        }
    }

    private void ResetSimulationAfterPreset()
    {
        if (canvasPainter != null)
        {
            canvasPainter.ResetCanvas();
        }

        if (particleSimulator != null)
        {
            particleSimulator.ResetParticles();
        }

        if (paintEmitter != null)
        {
            paintEmitter.ResetEmitter();
        }

        if (pendulumController != null)
        {
            pendulumController.ResetSimulation();
        }
    }
}