using UnityEngine;

public static class PaintImpactClassifier
{
    public static PaintImpactType Classify(
        PaintImpactData impact,
        PaintSurfaceProfile surfaceProfile
    )
    {
        float speed = impact.ImpactSpeed;
        float normalSpeed = impact.NormalSpeed;
        float tangentialSpeed = impact.TangentialSpeed;
        float grazing = impact.GrazingRatio01;
        float viscosity = Mathf.Max(impact.viscosity, 0.1f);
        float radius = Mathf.Max(impact.particleRadius, 0.001f);
        float energy = impact.ImpactEnergy;

        float absorption = surfaceProfile != null ? surfaceProfile.absorption : 0.35f;
        float roughness = surfaceProfile != null ? surfaceProfile.roughness : 0.55f;
        float splashFactor = surfaceProfile != null ? surfaceProfile.splashFactor : 1f;

        bool smallParticle = radius < 0.018f;
        bool thickPaint = viscosity > 1.35f;
        bool lowViscosity = viscosity < 0.75f;
        bool slipperySurface = absorption < 0.2f && roughness < 0.35f;

        if (smallParticle && speed > 2.0f)
        {
            return PaintImpactType.MistImpact;
        }

        if (thickPaint && speed < 2.2f)
        {
            return PaintImpactType.HeavyBlob;
        }

        if (grazing > 0.72f && tangentialSpeed > 1.4f)
        {
            if (slipperySurface)
            {
                return PaintImpactType.SkidImpact;
            }

            return PaintImpactType.GrazingSmear;
        }

        if (energy * splashFactor > 0.035f && normalSpeed > 1.8f)
        {
            if (lowViscosity || speed > 3.0f)
            {
                return PaintImpactType.HardSplash;
            }
        }

        if (speed < 0.9f)
        {
            return PaintImpactType.SoftDeposit;
        }

        return PaintImpactType.NormalSplat;
    }
}