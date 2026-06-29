using UnityEngine;

public class PaintImpactEngineV2 : MonoBehaviour
{
    [Header("References")]
    public PaintSurfaceStateV2 surfaceState;
    public CanvasPainter legacyCanvasPainter;
    public PaintSurfaceProfile surfaceProfile;
    public AdvancedSplatGeneratorV2 advancedSplatGenerator;

    [Header("Legacy Output")]
    public bool useLegacyCanvasOutput = true;
    public bool applySurfaceProfileToLegacyCanvas = true;

    [Header("Debug")]
    public bool logImpactTypes = false;
    public bool logOnlyWhenTypeChanges = true;

    [Header("Runtime Stats")]
    public int totalImpacts;
    public PaintImpactType lastImpactType;
    public float lastImpactEnergy;
    public float lastImpactSpeed;
    public string lastSurfaceName;

    private PaintImpactType previousLoggedType;
    private bool hasLoggedType;

    void Awake()
    {
        if (surfaceState == null)
        {
            surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        }

        if (legacyCanvasPainter == null)
        {
            legacyCanvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (advancedSplatGenerator == null)
        {
            advancedSplatGenerator = Object.FindFirstObjectByType<AdvancedSplatGeneratorV2>();
        }

        UpdateSurfaceName();
    }

    public void ProcessImpact(PaintImpactData impactData)
    {
        totalImpacts++;

        PaintImpactType impactType = PaintImpactClassifier.Classify(
            impactData,
            surfaceProfile
        );

        lastImpactType = impactType;
        lastImpactEnergy = impactData.ImpactEnergy;
        lastImpactSpeed = impactData.ImpactSpeed;

        if (surfaceState != null)
        {
            surfaceState.RegisterImpact(
                impactData,
                impactType,
                surfaceProfile
            );
        }

        UpdateSurfaceName();
        LogImpactIfNeeded(impactType);

        if (useLegacyCanvasOutput)
        {
            if (advancedSplatGenerator != null)
            {
                advancedSplatGenerator.GenerateSplat(
                    impactData,
                    impactType,
                    surfaceProfile
                );
            }
            else if (legacyCanvasPainter != null)
            {
                PaintUsingLegacyCanvas(impactData, impactType);
            }
        }
    }

    private void PaintUsingLegacyCanvas(
        PaintImpactData impactData,
        PaintImpactType impactType
    )
    {
        float radiusMultiplier = 1f;
        float velocityMultiplier = 1f;

        switch (impactType)
        {
            case PaintImpactType.SoftDeposit:
                radiusMultiplier = 0.75f;
                velocityMultiplier = 0.4f;
                break;

            case PaintImpactType.NormalSplat:
                radiusMultiplier = 1.0f;
                velocityMultiplier = 1.0f;
                break;

            case PaintImpactType.HardSplash:
                radiusMultiplier = 1.35f;
                velocityMultiplier = 1.25f;
                break;

            case PaintImpactType.GrazingSmear:
                radiusMultiplier = 0.9f;
                velocityMultiplier = 1.8f;
                break;

            case PaintImpactType.MistImpact:
                radiusMultiplier = 0.45f;
                velocityMultiplier = 1.1f;
                break;

            case PaintImpactType.HeavyBlob:
                radiusMultiplier = 1.55f;
                velocityMultiplier = 0.35f;
                break;

            case PaintImpactType.SkidImpact:
                radiusMultiplier = 0.8f;
                velocityMultiplier = 2.2f;
                break;
        }

        if (applySurfaceProfileToLegacyCanvas && surfaceProfile != null)
        {
            radiusMultiplier *= Mathf.Lerp(0.65f, 1.45f, Mathf.Clamp01(surfaceProfile.spreadFactor / 2f));
            velocityMultiplier *= Mathf.Lerp(0.45f, 2.0f, Mathf.Clamp01(surfaceProfile.smearFactor / 2f));

            if (impactType == PaintImpactType.HardSplash || impactType == PaintImpactType.MistImpact)
            {
                radiusMultiplier *= Mathf.Lerp(0.55f, 1.45f, Mathf.Clamp01(surfaceProfile.splashFactor / 2f));
            }

            if (impactType == PaintImpactType.HeavyBlob)
            {
                radiusMultiplier *= Mathf.Lerp(0.85f, 1.25f, 1f - surfaceProfile.absorption);
            }
        }

        legacyCanvasPainter.PaintImpactAtWorldPosition(
            impactData.worldPosition,
            impactData.incomingVelocity * velocityMultiplier,
            impactData.paintColor,
            impactData.particleRadius * radiusMultiplier
        );
    }

    private void LogImpactIfNeeded(PaintImpactType impactType)
    {
        if (!logImpactTypes)
        {
            return;
        }

        if (logOnlyWhenTypeChanges)
        {
            if (hasLoggedType && impactType == previousLoggedType)
            {
                return;
            }

            previousLoggedType = impactType;
            hasLoggedType = true;
        }

        Debug.Log(
            "Impact V2 | Type: " + impactType +
            " | Surface: " + lastSurfaceName +
            " | Speed: " + lastImpactSpeed.ToString("0.00") +
            " | Energy: " + lastImpactEnergy.ToString("0.0000")
        );
    }

    private void UpdateSurfaceName()
    {
        if (surfaceProfile != null)
        {
            lastSurfaceName = surfaceProfile.surfaceName;
        }
        else
        {
            lastSurfaceName = "None";
        }
    }

    public void SetSurfaceProfile(PaintSurfaceProfile newProfile)
    {
        surfaceProfile = newProfile;
        UpdateSurfaceName();
    }

    public void ResetImpactStats()
    {
        totalImpacts = 0;
        lastImpactType = PaintImpactType.NormalSplat;
        lastImpactEnergy = 0f;
        lastImpactSpeed = 0f;
        hasLoggedType = false;
        previousLoggedType = PaintImpactType.NormalSplat;
        UpdateSurfaceName(); 

        if (surfaceState != null)
        {
            surfaceState.ResetState();
        }                                                              
    }
}