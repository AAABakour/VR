using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(160)]
public class SceneReferenceValidator : MonoBehaviour
{
    [Header("Validation")]
    public bool validateOnStart = true;
    public bool autoResolveMissingReferences = true;
    public bool validateLegacySceneReferences = true;
    public bool validatePhaseTwoRigReferences = true;
    public bool validatePhaseThreeFluidBoxReferences = false;
    public bool validatePhaseFourBucketSphReferences = false;

    [Header("Expected Scene Objects")]
    public Transform pivotPoint;
    public Transform bucket;
    public Transform rope;
    public Transform paintNozzle;
    public Transform canvasBoard;
    public GameObject mainUI;
    public GameObject eventSystem;

    [Header("Expected Systems")]
    public SimulationManager simulationManager;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator paintParticleSimulator;
    public CanvasPainter canvasPainter;
    public PendulumController pendulumController;

    [Header("Phase 2 Rig Systems")]
    public RopeRigController ropeRigController;
    public BucketRigController bucketRigController;
    public BucketHandleRig bucketHandleRig;
    public BucketMotionDataProvider bucketMotionDataProvider;
    public BucketCollisionProxy bucketCollisionProxy;
    public RopeRigSubsystemAdapter ropeRigSubsystemAdapter;
    public BucketRigSubsystemAdapter bucketRigSubsystemAdapter;

    [Header("Phase 3 Fluid Box Systems")]
    public GpuSphSolver gpuSphSolver;
    public GpuSphParticleRenderer gpuSphParticleRenderer;
    public FluidBoxController fluidBoxController;
    public FluidBoxMotionController fluidBoxMotionController;
    public FluidBoxBenchmarkController fluidBoxBenchmarkController;
    public FluidBoxBenchmarkUI fluidBoxBenchmarkUI;

    [Header("Phase 4 Bucket SPH Systems")]
    public BucketSphFluidController bucketSphFluidController;
    public BucketSphNozzleEmitter bucketSphNozzleEmitter;
    public BucketSphCollisionProvider bucketSphCollisionProvider;
    public BucketSphDebugUI bucketSphDebugUI;
    public BucketSphSubsystemAdapter bucketSphSubsystemAdapter;

    private void Start()
    {
        if (validateOnStart)
        {
            ValidateSceneReferences();
        }
    }

    [ContextMenu("Validate Scene References")]
    public void ValidateSceneReferences()
    {
        if (autoResolveMissingReferences)
        {
            ResolveMissingReferences();
        }

        List<string> warnings = new List<string>();

        if (validateLegacySceneReferences)
        {
            RequireReference(warnings, pivotPoint, "PivotPoint transform");
            RequireReference(warnings, bucket, "Bucket transform");
            RequireReference(warnings, rope, "Rope transform");
            RequireReference(warnings, paintNozzle, "PaintNozzle transform");
            RequireReference(warnings, canvasBoard, "CanvasBoard transform");
            RequireReference(warnings, simulationManager, "SimulationManager");
            RequireReference(warnings, paintEmitter, "PaintEmitter");
            RequireReference(warnings, paintParticleSimulator, "PaintParticleSimulator");
            RequireReference(warnings, canvasPainter, "CanvasPainter");
            RequireReference(warnings, mainUI, "MainUI");
            RequireReference(warnings, eventSystem, "EventSystem");

            ValidatePendulumReferences(warnings);
            ValidatePaintReferences(warnings);
            ValidateManagerReferences(warnings);
        }

        if (validatePhaseTwoRigReferences)
        {
            ValidatePhaseTwoRigReferences(warnings);
        }

        if (validatePhaseThreeFluidBoxReferences)
        {
            ValidatePhaseThreeFluidBoxReferences(warnings);
        }

        if (validatePhaseFourBucketSphReferences)
        {
            ValidatePhaseFourBucketSphReferences(warnings);
        }

        if (warnings.Count == 0)
        {
            Debug.Log("[SceneReferenceValidator] Requested scene references are present.", this);
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[SceneReferenceValidator] Missing or incomplete scene references:");

        for (int i = 0; i < warnings.Count; i++)
        {
            builder.Append("- ");
            builder.AppendLine(warnings[i]);
        }

        Debug.LogWarning(builder.ToString(), this);
    }

    private void ResolveMissingReferences()
    {
        if (pivotPoint == null)
        {
            pivotPoint = FindTransformByName("PivotPoint");
        }

        if (bucket == null)
        {
            bucket = FindTransformByName("Bucket");
        }

        if (rope == null)
        {
            rope = FindTransformByName("Rope");
        }

        if (paintNozzle == null)
        {
            paintNozzle = FindTransformByName("PaintNozzle");
        }

        if (canvasBoard == null)
        {
            canvasBoard = FindTransformByName("CanvasBoard");
        }

        if (mainUI == null)
        {
            mainUI = GameObject.Find("MainUI");
        }

        if (eventSystem == null)
        {
            eventSystem = GameObject.Find("EventSystem");
        }

        if (simulationManager == null)
        {
            simulationManager = Object.FindFirstObjectByType<SimulationManager>();
        }

        if (paintEmitter == null)
        {
            paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (paintParticleSimulator == null)
        {
            paintParticleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        }

        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (pendulumController == null)
        {
            pendulumController = Object.FindFirstObjectByType<PendulumController>();
        }

        if (ropeRigController == null)
        {
            ropeRigController = Object.FindFirstObjectByType<RopeRigController>();
        }

        if (bucketRigController == null)
        {
            bucketRigController = Object.FindFirstObjectByType<BucketRigController>();
        }

        if (bucketHandleRig == null)
        {
            bucketHandleRig = Object.FindFirstObjectByType<BucketHandleRig>();
        }

        if (bucketMotionDataProvider == null)
        {
            bucketMotionDataProvider = Object.FindFirstObjectByType<BucketMotionDataProvider>();
        }

        if (bucketCollisionProxy == null)
        {
            bucketCollisionProxy = Object.FindFirstObjectByType<BucketCollisionProxy>();
        }

        if (ropeRigSubsystemAdapter == null)
        {
            ropeRigSubsystemAdapter = Object.FindFirstObjectByType<RopeRigSubsystemAdapter>();
        }

        if (bucketRigSubsystemAdapter == null)
        {
            bucketRigSubsystemAdapter = Object.FindFirstObjectByType<BucketRigSubsystemAdapter>();
        }

        if (gpuSphSolver == null)
        {
            gpuSphSolver = Object.FindFirstObjectByType<GpuSphSolver>();
        }

        if (gpuSphParticleRenderer == null)
        {
            gpuSphParticleRenderer = Object.FindFirstObjectByType<GpuSphParticleRenderer>();
        }

        if (fluidBoxController == null)
        {
            fluidBoxController = Object.FindFirstObjectByType<FluidBoxController>();
        }

        if (fluidBoxMotionController == null)
        {
            fluidBoxMotionController = Object.FindFirstObjectByType<FluidBoxMotionController>();
        }

        if (fluidBoxBenchmarkController == null)
        {
            fluidBoxBenchmarkController = Object.FindFirstObjectByType<FluidBoxBenchmarkController>();
        }

        if (fluidBoxBenchmarkUI == null)
        {
            fluidBoxBenchmarkUI = Object.FindFirstObjectByType<FluidBoxBenchmarkUI>();
        }

        if (bucketSphFluidController == null)
        {
            bucketSphFluidController = Object.FindFirstObjectByType<BucketSphFluidController>();
        }

        if (bucketSphNozzleEmitter == null)
        {
            bucketSphNozzleEmitter = Object.FindFirstObjectByType<BucketSphNozzleEmitter>();
        }

        if (bucketSphCollisionProvider == null)
        {
            bucketSphCollisionProvider = Object.FindFirstObjectByType<BucketSphCollisionProvider>();
        }

        if (bucketSphDebugUI == null)
        {
            bucketSphDebugUI = Object.FindFirstObjectByType<BucketSphDebugUI>();
        }

        if (bucketSphSubsystemAdapter == null)
        {
            bucketSphSubsystemAdapter = Object.FindFirstObjectByType<BucketSphSubsystemAdapter>();
        }
    }

    private void ValidatePendulumReferences(List<string> warnings)
    {
        if (pendulumController == null)
        {
            return;
        }

        RequireReference(warnings, pendulumController.pivotPoint, "PendulumController.pivotPoint");
        RequireReference(warnings, pendulumController.bucket, "PendulumController.bucket");
        RequireReference(warnings, pendulumController.rope, "PendulumController.rope");
    }

    private void ValidatePaintReferences(List<string> warnings)
    {
        if (paintEmitter != null)
        {
            RequireReference(warnings, paintEmitter.particleSimulator, "PaintEmitter.particleSimulator");
            RequireReference(warnings, paintEmitter.canvasPainter, "PaintEmitter.canvasPainter");
        }

        if (paintParticleSimulator != null)
        {
            RequireReference(warnings, paintParticleSimulator.canvasPainter, "PaintParticleSimulator.canvasPainter");
        }

        if (canvasPainter != null && canvasPainter.GetComponent<Renderer>() == null)
        {
            warnings.Add("CanvasPainter GameObject has no Renderer component.");
        }
    }

    private void ValidateManagerReferences(List<string> warnings)
    {
        if (simulationManager == null)
        {
            return;
        }

        RequireReference(warnings, simulationManager.pendulumController, "SimulationManager.pendulumController");
        RequireReference(warnings, simulationManager.paintEmitter, "SimulationManager.paintEmitter");
        RequireReference(warnings, simulationManager.canvasPainter, "SimulationManager.canvasPainter");
    }

    private void ValidatePhaseTwoRigReferences(List<string> warnings)
    {
        if (ropeRigController != null)
        {
            RequireReference(warnings, ropeRigController.startPoint, "RopeRigController.startPoint");
            RequireReference(warnings, ropeRigController.endPoint, "RopeRigController.endPoint");
        }

        if (bucketRigController != null)
        {
            RequireReference(warnings, bucketRigController.bucketRoot, "BucketRigController.bucketRoot");
            RequireReference(warnings, bucketRigController.ropeAttachPoint, "BucketRigController.ropeAttachPoint");
            RequireReference(warnings, bucketRigController.nozzlePoint, "BucketRigController.nozzlePoint");
            RequireReference(warnings, bucketRigController.motionDataProvider, "BucketRigController.motionDataProvider");
            RequireReference(warnings, bucketRigController.bucketCollisionProxy, "BucketRigController.bucketCollisionProxy");
        }

        if (bucketHandleRig != null)
        {
            RequireReference(warnings, bucketHandleRig.bucketRoot, "BucketHandleRig.bucketRoot");
            RequireReference(warnings, bucketHandleRig.handlePivot, "BucketHandleRig.handlePivot");
        }

        if (bucketMotionDataProvider != null)
        {
            RequireReference(warnings, bucketMotionDataProvider.bucketTransform, "BucketMotionDataProvider.bucketTransform");
        }

        if (bucketCollisionProxy != null)
        {
            RequireReference(warnings, bucketCollisionProxy.bucketRoot, "BucketCollisionProxy.bucketRoot");
        }

        if (ropeRigSubsystemAdapter != null)
        {
            RequireReference(warnings, ropeRigSubsystemAdapter.ropeRigController, "RopeRigSubsystemAdapter.ropeRigController");
        }

        if (bucketRigSubsystemAdapter != null)
        {
            RequireReference(warnings, bucketRigSubsystemAdapter.bucketRigController, "BucketRigSubsystemAdapter.bucketRigController");
            RequireReference(warnings, bucketRigSubsystemAdapter.bucketHandleRig, "BucketRigSubsystemAdapter.bucketHandleRig");
            RequireReference(warnings, bucketRigSubsystemAdapter.motionDataProvider, "BucketRigSubsystemAdapter.motionDataProvider");
            RequireReference(warnings, bucketRigSubsystemAdapter.collisionProxy, "BucketRigSubsystemAdapter.collisionProxy");
        }
    }

    private void ValidatePhaseThreeFluidBoxReferences(List<string> warnings)
    {
        RequireReference(warnings, gpuSphSolver, "GpuSphSolver");
        RequireReference(warnings, gpuSphParticleRenderer, "GpuSphParticleRenderer");
        RequireReference(warnings, fluidBoxController, "FluidBoxController");
        RequireReference(warnings, fluidBoxMotionController, "FluidBoxMotionController");
        RequireReference(warnings, fluidBoxBenchmarkController, "FluidBoxBenchmarkController");
        RequireReference(warnings, fluidBoxBenchmarkUI, "FluidBoxBenchmarkUI");

        if (gpuSphSolver != null)
        {
            RequireReference(warnings, gpuSphSolver.sphComputeShader, "GpuSphSolver.sphComputeShader");
            RequireReference(warnings, gpuSphSolver.settingsTemplate, "GpuSphSolver.settingsTemplate");
            RequireReference(warnings, gpuSphSolver.fluidBox, "GpuSphSolver.fluidBox");
            RequireReference(warnings, gpuSphSolver.debugStats, "GpuSphSolver.debugStats");
        }

        if (gpuSphParticleRenderer != null)
        {
            RequireReference(warnings, gpuSphParticleRenderer.solver, "GpuSphParticleRenderer.solver");
            RequireReference(warnings, gpuSphParticleRenderer.fluidBox, "GpuSphParticleRenderer.fluidBox");
            RequireReference(warnings, gpuSphParticleRenderer.particleMaterial, "GpuSphParticleRenderer.particleMaterial");
        }

        if (fluidBoxController != null)
        {
            RequireReference(warnings, fluidBoxController.glassMaterialTemplate, "FluidBoxController.glassMaterialTemplate");
        }

        if (fluidBoxBenchmarkController != null)
        {
            RequireReference(warnings, fluidBoxBenchmarkController.solver, "FluidBoxBenchmarkController.solver");
            RequireReference(warnings, fluidBoxBenchmarkController.fluidBox, "FluidBoxBenchmarkController.fluidBox");
            RequireReference(warnings, fluidBoxBenchmarkController.motionController, "FluidBoxBenchmarkController.motionController");
            RequireReference(warnings, fluidBoxBenchmarkController.debugStats, "FluidBoxBenchmarkController.debugStats");
        }

        if (fluidBoxBenchmarkUI != null)
        {
            RequireReference(warnings, fluidBoxBenchmarkUI.benchmarkController, "FluidBoxBenchmarkUI.benchmarkController");
            RequireReference(warnings, fluidBoxBenchmarkUI.stats, "FluidBoxBenchmarkUI.stats");
            RequireReference(warnings, fluidBoxBenchmarkUI.motionController, "FluidBoxBenchmarkUI.motionController");
        }
    }

    private void ValidatePhaseFourBucketSphReferences(List<string> warnings)
    {
        RequireReference(warnings, GameObject.Find("SPH_BucketFluidRoot"), "SPH_BucketFluidRoot GameObject");
        RequireReference(warnings, bucketSphFluidController, "BucketSphFluidController");
        RequireReference(warnings, bucketSphNozzleEmitter, "BucketSphNozzleEmitter");
        RequireReference(warnings, bucketSphCollisionProvider, "BucketSphCollisionProvider");
        RequireReference(warnings, bucketSphDebugUI, "BucketSphDebugUI");
        RequireReference(warnings, bucketSphSubsystemAdapter, "BucketSphSubsystemAdapter");
        RequireReference(warnings, gpuSphSolver, "GpuSphSolver");
        RequireReference(warnings, gpuSphParticleRenderer, "GpuSphParticleRenderer");

        if (bucketSphFluidController != null)
        {
            RequireReference(warnings, bucketSphFluidController.solver, "BucketSphFluidController.solver");
            RequireReference(warnings, bucketSphFluidController.particleRenderer, "BucketSphFluidController.particleRenderer");
            RequireReference(warnings, bucketSphFluidController.collisionProvider, "BucketSphFluidController.collisionProvider");
            RequireReference(warnings, bucketSphFluidController.nozzleEmitter, "BucketSphFluidController.nozzleEmitter");
            RequireReference(warnings, bucketSphFluidController.bucketRigController, "BucketSphFluidController.bucketRigController");
            RequireReference(warnings, bucketSphFluidController.bucketMotionDataProvider, "BucketSphFluidController.bucketMotionDataProvider");
        }

        if (bucketSphNozzleEmitter != null)
        {
            RequireReference(warnings, bucketSphNozzleEmitter.solver, "BucketSphNozzleEmitter.solver");
            RequireReference(warnings, bucketSphNozzleEmitter.collisionProvider, "BucketSphNozzleEmitter.collisionProvider");
            RequireReference(warnings, bucketSphNozzleEmitter.nozzlePoint, "BucketSphNozzleEmitter.nozzlePoint");
            RequireReference(warnings, bucketSphNozzleEmitter.bucketMotionDataProvider, "BucketSphNozzleEmitter.bucketMotionDataProvider");
        }

        if (bucketSphCollisionProvider != null)
        {
            RequireReference(warnings, bucketSphCollisionProvider.bucketRigController, "BucketSphCollisionProvider.bucketRigController");
            RequireReference(warnings, bucketSphCollisionProvider.bucketCollisionProxy, "BucketSphCollisionProvider.bucketCollisionProxy");
            RequireReference(warnings, bucketSphCollisionProvider.motionDataProvider, "BucketSphCollisionProvider.motionDataProvider");
            RequireReference(warnings, bucketSphCollisionProvider.bucketRoot, "BucketSphCollisionProvider.bucketRoot");
            RequireReference(warnings, bucketSphCollisionProvider.nozzlePoint, "BucketSphCollisionProvider.nozzlePoint");
        }

        if (bucketSphDebugUI != null)
        {
            RequireReference(warnings, bucketSphDebugUI.controller, "BucketSphDebugUI.controller");
            RequireReference(warnings, bucketSphDebugUI.stats, "BucketSphDebugUI.stats");
            RequireReference(warnings, bucketSphDebugUI.nozzleEmitter, "BucketSphDebugUI.nozzleEmitter");

            if (!bucketSphDebugUI.autoCreateUi)
            {
                warnings.Add("BucketSphDebugUI.autoCreateUi is disabled; Phase 4 demo may not show SPH stats.");
            }
        }

        if (bucketSphSubsystemAdapter != null)
        {
            RequireReference(warnings, bucketSphSubsystemAdapter.controller, "BucketSphSubsystemAdapter.controller");
        }

        if (gpuSphSolver != null)
        {
            RequireReference(warnings, gpuSphSolver.sphComputeShader, "GpuSphSolver.sphComputeShader");
            RequireReference(warnings, gpuSphSolver.settingsTemplate, "GpuSphSolver.settingsTemplate");
            RequireReference(warnings, gpuSphSolver.bucketCollisionProvider, "GpuSphSolver.bucketCollisionProvider");
            RequireReference(warnings, gpuSphSolver.debugStats, "GpuSphSolver.debugStats");

            if (Application.isPlaying && !gpuSphSolver.IsInitialized)
            {
                warnings.Add("GpuSphSolver is not initialized in Play Mode.");
            }
        }

        if (gpuSphParticleRenderer != null)
        {
            RequireReference(warnings, gpuSphParticleRenderer.solver, "GpuSphParticleRenderer.solver");
            RequireReference(warnings, gpuSphParticleRenderer.particleMaterial, "GpuSphParticleRenderer.particleMaterial");
            RequireReference(warnings, gpuSphParticleRenderer.renderCamera, "GpuSphParticleRenderer.renderCamera");

            if (gpuSphParticleRenderer.particleSize <= 0f)
            {
                warnings.Add("GpuSphParticleRenderer.particleSize is zero or negative.");
            }
        }
    }

    private void RequireReference(List<string> warnings, Object reference, string label)
    {
        if (reference == null)
        {
            warnings.Add(label + " is not assigned or could not be found.");
        }
    }

    private Transform FindTransformByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }
}
