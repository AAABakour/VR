using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationManager : MonoBehaviour
{
    [Header("Simulation References")]
    public PendulumController pendulumController;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;
    public PaintImpactEngineV2 impactEngine;
    public PaintSurfaceStateV2 surfaceState;
    public PaintDripSolverV2 dripSolver;
    public PaintFilmFluidSolverV2 fluidFilmSolver;
    public PaintThicknessRendererV2 thicknessRenderer;
    public BucketInteriorLiquidSystemV2 bucketLiquid;
    public SphParticleBudgetController sphBudgetController;
    public SphCollisionSurfaceBridge sphCollisionBridge;

    [Header("Keyboard Controls")]
    public Key resetKey = Key.R;

    void Awake()
    {
        AutoFindReferences();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[resetKey].wasPressedThisFrame)
        {
            ResetSimulation();
        }
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

        if (bucketLiquid == null)
        {
            bucketLiquid = Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
        }

        if (sphBudgetController == null)
        {
            sphBudgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
        }

        if (sphCollisionBridge == null)
        {
            sphCollisionBridge = Object.FindFirstObjectByType<SphCollisionSurfaceBridge>();
        }
    }

    public void ResetSimulation()
    {
        AutoFindReferences();

        if (particleSimulator != null)
        {
            particleSimulator.ResetParticles();
        }

        if (canvasPainter != null)
        {
            canvasPainter.ResetCanvas();
        }

        if (impactEngine != null)
        {
            impactEngine.ResetImpactStats();
        }
        else if (surfaceState != null)
        {
            surfaceState.ResetState();
        }

        if (dripSolver != null)
        {
            dripSolver.ResetSolver();
        }

        if (fluidFilmSolver != null)
        {
            fluidFilmSolver.ResetSolver();
        }

        if (thicknessRenderer != null)
        {
            thicknessRenderer.enableRaisedPaintMesh = false;
            thicknessRenderer.enabled = false;
        }

        if (pendulumController != null)
        {
            pendulumController.ResetSimulation();
        }

        if (paintEmitter != null)
        {
            paintEmitter.ResetEmitter();
        }

        if (bucketLiquid != null)
        {
            bucketLiquid.presentationBucketTransparency = false;
            bucketLiquid.renderLiquidVolume = false;
            bucketLiquid.RecalibrateNow();
            bucketLiquid.ResetLiquidVisual();
        }

        if (sphCollisionBridge != null)
        {
            sphCollisionBridge.ResetBridgeStats();
        }

        if (sphBudgetController != null)
        {
            sphBudgetController.ResetSphReadinessRuntimeStats();
            sphBudgetController.ApplyHighScalePreparation(false);
        }
    }
}
