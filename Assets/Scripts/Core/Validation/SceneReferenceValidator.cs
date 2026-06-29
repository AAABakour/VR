using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SceneReferenceValidator : MonoBehaviour
{
    [Header("Validation")]
    public bool validateOnStart = true;
    public bool autoResolveMissingReferences = true;

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
        ValidatePhaseTwoRigReferences(warnings);

        if (warnings.Count == 0)
        {
            Debug.Log("[SceneReferenceValidator] Required Phase 1 scene references are present.", this);
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
