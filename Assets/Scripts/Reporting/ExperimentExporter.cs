using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class ExperimentExporter : MonoBehaviour
{
    [Header("References")]
    public CanvasPainter canvasPainter;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public PendulumController pendulumController;
    public TextMeshProUGUI statusText;

    [Header("Export Settings")]
    public string exportFolderName = "SwingingPaintBucketExports";
    public string imagePrefix = "Canvas";
    public string reportPrefix = "ExperimentReport";

    void Awake()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (canvasPainter == null)
        {
            canvasPainter = UnityEngine.Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (paintEmitter == null)
        {
            paintEmitter = UnityEngine.Object.FindFirstObjectByType<PaintEmitter>();
        }

        if (particleSimulator == null)
        {
            particleSimulator = UnityEngine.Object.FindFirstObjectByType<PaintParticleSimulator>();
        }

        if (pendulumController == null)
        {
            pendulumController = UnityEngine.Object.FindFirstObjectByType<PendulumController>();
        }
    }

    public void SaveImage()
    {
        AutoFindReferences();

        if (canvasPainter == null)
        {
            ShowStatus("Save Image failed: CanvasPainter not found.");
            return;
        }

        canvasPainter.ForceApplyTexture();

        Texture2D texture = canvasPainter.GetCanvasTexture();

        if (texture == null)
        {
            ShowStatus("Save Image failed: canvas texture is null.");
            return;
        }

        string folderPath = GetExportFolderPath();
        string filePath = Path.Combine(folderPath, imagePrefix + "_" + GetTimestamp() + ".png");

        byte[] pngData = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);

        Debug.Log("Canvas image saved to: " + filePath);
        ShowStatus("Image saved:\n" + filePath);
    }

    public void SaveReport()
    {
        AutoFindReferences();

        string folderPath = GetExportFolderPath();
        string filePath = Path.Combine(folderPath, reportPrefix + "_" + GetTimestamp() + ".txt");

        string reportText = BuildReportText();

        File.WriteAllText(filePath, reportText, Encoding.UTF8);

        Debug.Log("Experiment report saved to: " + filePath);
        ShowStatus("Report saved:\n" + filePath);
    }

    public void SaveImageAndReport()
    {
        SaveImage();
        SaveReport();
    }

    private string BuildReportText()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Swinging Paint Bucket Simulation");
        report.AppendLine("Experiment Report");
        report.AppendLine("Generated At: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        report.AppendLine("1. General Simulation Summary");
        report.AppendLine("--------------------------------");

        if (canvasPainter != null)
        {
            float coverage = canvasPainter.GetPaintCoverage01() * 100f;
            float paintedArea = canvasPainter.GetApproxPaintedArea();

            report.AppendLine("Surface Type: " + canvasPainter.surfaceType);
            report.AppendLine("Texture Size: " + canvasPainter.textureSize);
            report.AppendLine("Painted Coverage: " + coverage.ToString("0.00") + " %");
            report.AppendLine("Approx Painted Area: " + paintedArea.ToString("0.000") + " square units");
            report.AppendLine("Painted Pixels: " + canvasPainter.CountPaintedPixels());
        }

        if (particleSimulator != null)
        {
            report.AppendLine("Active Particles: " + particleSimulator.ActiveParticleCount);
            report.AppendLine("Active Visual Droplets: " + particleSimulator.ActiveVisualDropletCount);
        }

        report.AppendLine();

        report.AppendLine("2. Pendulum / Bucket Motion Settings");
        report.AppendLine("--------------------------------");

        if (pendulumController != null)
        {
            report.AppendLine("Rope Length: " + pendulumController.ropeLength);
            report.AppendLine("Start Angle X: " + pendulumController.startAngleDegrees);
            report.AppendLine("Initial Angular Velocity X: " + pendulumController.initialAngularVelocity);
            report.AppendLine("Gravity: " + pendulumController.gravity);
            report.AppendLine("Damping: " + pendulumController.damping);

            report.AppendLine("Enable Depth Swing: " + pendulumController.enableDepthSwing);
            report.AppendLine("Start Angle Z: " + pendulumController.startAngleZDegrees);
            report.AppendLine("Initial Angular Velocity Z: " + pendulumController.initialAngularVelocityZ);
            report.AppendLine("Z Swing Strength: " + pendulumController.zSwingStrength);

            report.AppendLine("Rope Stretch Enabled: " + pendulumController.enableRopeStretch);
            report.AppendLine("Rope Elasticity: " + pendulumController.ropeElasticity);
            report.AppendLine("Max Rope Stretch: " + pendulumController.maxRopeStretch);

            report.AppendLine("Bucket Wobble Enabled: " + pendulumController.enableBucketWobble);
            report.AppendLine("Wobble Strength: " + pendulumController.wobbleStrength);
            report.AppendLine("Wobble Damping: " + pendulumController.wobbleDamping);

            report.AppendLine("Bucket Torsion Enabled: " + pendulumController.enableBucketTorsion);
            report.AppendLine("Torsion Strength: " + pendulumController.torsionStrength);
            report.AppendLine("Torsion Damping: " + pendulumController.torsionDamping);
        }

        report.AppendLine();

        report.AppendLine("3. Paint Emitter Settings");
        report.AppendLine("--------------------------------");

        if (paintEmitter != null)
        {
            report.AppendLine("Remaining Paint Amount: " + paintEmitter.RemainingPaintAmount.ToString("0.000"));
            report.AppendLine("Paint Fill Percent: " + (paintEmitter.PaintFill01 * 100f).ToString("0.00") + " %");

            report.AppendLine("Initial Paint Amount: " + paintEmitter.initialPaintAmount);
            report.AppendLine("Hole Diameter: " + paintEmitter.holeDiameter);
            report.AppendLine("Viscosity: " + paintEmitter.viscosity);
            report.AppendLine("Nozzle Shape: " + paintEmitter.CurrentNozzleShapeName);

            report.AppendLine("Base Flow Rate: " + paintEmitter.baseFlowRate);
            report.AppendLine("Reference Hole Diameter: " + paintEmitter.referenceHoleDiameter);

            report.AppendLine("Particles Per Unit Flow: " + paintEmitter.particlesPerUnitFlow);
            report.AppendLine("Downward Start Speed: " + paintEmitter.downwardStartSpeed);
            report.AppendLine("Nozzle Velocity Influence: " + paintEmitter.nozzleVelocityInfluence);
            report.AppendLine("Random Spread: " + paintEmitter.randomSpread);

            report.AppendLine("Internal Slosh Enabled: " + paintEmitter.enableInternalSlosh);
            report.AppendLine("Slosh Strength: " + paintEmitter.sloshStrength);
            report.AppendLine("Max Slosh Offset: " + paintEmitter.maxSloshOffset);
            report.AppendLine("Slosh Response Time: " + paintEmitter.sloshResponseTime);
            report.AppendLine("Slosh Flow Influence: " + paintEmitter.sloshFlowInfluence);
            report.AppendLine("Slosh Direction Influence: " + paintEmitter.sloshDirectionInfluence);
            report.AppendLine("Flow Noise Amount: " + paintEmitter.flowNoiseAmount);
            report.AppendLine("Flow Noise Speed: " + paintEmitter.flowNoiseSpeed);
        }

        report.AppendLine();

        report.AppendLine("4. Particle Simulation Settings");
        report.AppendLine("--------------------------------");

        if (particleSimulator != null)
        {
            report.AppendLine("Max Particles: " + particleSimulator.maxParticles);

            report.AppendLine("Particle Interaction Enabled: " + particleSimulator.enableParticleInteraction);
            report.AppendLine("Interaction Radius: " + particleSimulator.interactionRadius);
            report.AppendLine("Cohesion Strength: " + particleSimulator.cohesionStrength);
            report.AppendLine("Separation Strength: " + particleSimulator.separationStrength);
            report.AppendLine("Viscosity Alignment: " + particleSimulator.viscosityAlignment);
            report.AppendLine("Max Interaction Checks: " + particleSimulator.maxInteractionChecks);

            report.AppendLine("Air Turbulence Enabled: " + particleSimulator.enableAirTurbulence);
            report.AppendLine("Wind Direction: " + particleSimulator.windDirection);
            report.AppendLine("Wind Strength: " + particleSimulator.windStrength);
            report.AppendLine("Turbulence Strength: " + particleSimulator.turbulenceStrength);
            report.AppendLine("Turbulence Frequency: " + particleSimulator.turbulenceFrequency);
            report.AppendLine("Turbulence Vertical Influence: " + particleSimulator.turbulenceVerticalInfluence);

            report.AppendLine("Visual Droplets Enabled: " + particleSimulator.showVisualDroplets);
            report.AppendLine("Visual Pool Size: " + particleSimulator.visualPoolSize);
        }

        report.AppendLine();

        report.AppendLine("5. Canvas Painting Settings");
        report.AppendLine("--------------------------------");

        if (canvasPainter != null)
        {
            report.AppendLine("Surface Type: " + canvasPainter.surfaceType);
            report.AppendLine("Paint Opacity: " + canvasPainter.paintOpacity);
            report.AppendLine("Edge Irregularity: " + canvasPainter.edgeIrregularity);
            report.AppendLine("Spray Amount: " + canvasPainter.sprayAmount);
            report.AppendLine("Spray Spread: " + canvasPainter.spraySpread);
            report.AppendLine("Directional Smear Enabled: " + canvasPainter.enableDirectionalSmear);
            report.AppendLine("Smear Length: " + canvasPainter.smearLength);
            report.AppendLine("Smear Steps: " + canvasPainter.smearSteps);
        }

        report.AppendLine();

        report.AppendLine("6. Implementation Notes");
        report.AppendLine("--------------------------------");
        report.AppendLine("- Bucket motion is calculated manually using a custom pendulum solver.");
        report.AppendLine("- Paint is represented as manually simulated particles.");
        report.AppendLine("- Particle interaction includes cohesion, separation, and viscosity alignment.");
        report.AppendLine("- Paint impact is not drawn as a perfect circle; it uses irregular splats, spray, and directional smear.");
        report.AppendLine("- Different canvas surface types affect spread, opacity, edge roughness, and smearing.");
        report.AppendLine("- Visual droplets use object pooling to avoid runtime Instantiate/Destroy overhead.");
        report.AppendLine("- Texture painting is optimized using a Color32 pixel buffer and delayed texture Apply.");

        return report.ToString();
    }

    private string GetExportFolderPath()
    {
        string folderPath = Path.Combine(Application.persistentDataPath, exportFolderName);

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        return folderPath;
    }

    private string GetTimestamp()
    {
        return DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    private void ShowStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}