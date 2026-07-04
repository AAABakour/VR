using UnityEngine;

public class AdvancedSplatGeneratorV2 : MonoBehaviour
{
    [Header("References")]
    public CanvasPainter canvasPainter;

    [Header("Main Blob")]
    public float baseBlobMultiplier = 1.0f;
    public float heavyBlobMultiplier = 1.7f;
    public float hardSplashMultiplier = 1.25f;
    public float mistMultiplier = 0.45f;

    [Header("Satellite Droplets")]
    public int baseSatelliteCount = 4;
    public int hardSplashSatelliteCount = 18;
    public int mistSatelliteCount = 24;
    public float satelliteMinDistance = 1.2f;
    public float satelliteMaxDistance = 4.0f;
    public float satelliteRadiusMin = 0.12f;
    public float satelliteRadiusMax = 0.38f;

    [Header("Filament Streaks")]
    public int baseFilamentCount = 3;
    public int hardSplashFilamentCount = 9;
    public float filamentLengthMin = 1.5f;
    public float filamentLengthMax = 4.5f;
    public float filamentRadiusMultiplier = 0.22f;

    [Header("Directional Smear")]
    public int smearSteps = 6;
    public float grazingSmearLength = 4.0f;
    public float skidSmearLength = 6.0f;
    public float smearRadiusFalloff = 0.72f;

    [Header("Wet Halo")]
    public bool enableWetHalo = true;
    public Color wetHaloColor = new Color(1f, 0.15f, 0.08f, 1f);
    public float wetHaloRadiusMultiplier = 1.8f;
    public float wetHaloOpacityMultiplier = 0.28f;

    [Header("Performance")]
    public int maxGeneratedMarksPerImpact = 42;

    void Awake()
    {
        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }
    }

    public void GenerateSplat(
        PaintImpactData impact,
        PaintImpactType impactType,
        PaintSurfaceProfile surfaceProfile
    )
    {
        if (canvasPainter == null)
        {
            return;
        }

        SurfaceFactors factors = BuildSurfaceFactors(surfaceProfile);

        Vector3 normal = impact.SafeNormal;
        Vector3 tangent = GetMainTangent(impact, normal);
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        float baseRadius = Mathf.Max(impact.particleRadius, 0.003f);
        float speed = impact.ImpactSpeed;
        float tangentialSpeed = impact.TangentialSpeed;
        float energy01 = Mathf.Clamp01(impact.ImpactEnergy * 18f);

        int generatedMarks = 0;

        DrawWetHaloIfNeeded(
            impact,
            impactType,
            factors,
            baseRadius,
            energy01,
            ref generatedMarks
        );

        DrawMainBlob(
            impact,
            impactType,
            factors,
            baseRadius,
            tangent,
            ref generatedMarks
        );

        DrawDirectionalSmearIfNeeded(
            impact,
            impactType,
            factors,
            baseRadius,
            tangent,
            bitangent,
            tangentialSpeed,
            ref generatedMarks
        );

        DrawSatelliteDroplets(
            impact,
            impactType,
            factors,
            baseRadius,
            tangent,
            bitangent,
            speed,
            energy01,
            ref generatedMarks
        );

        DrawFilamentsIfNeeded(
            impact,
            impactType,
            factors,
            baseRadius,
            tangent,
            bitangent,
            energy01,
            ref generatedMarks
        );
    }

    private struct SurfaceFactors
    {
        public float absorption;
        public float roughness;
        public float spread;
        public float smear;
        public float splash;
        public float edgeNoise;
        public float staining;
    }

    private SurfaceFactors BuildSurfaceFactors(PaintSurfaceProfile profile)
    {
        SurfaceFactors factors = new SurfaceFactors
        {
            absorption = 0.35f,
            roughness = 0.55f,
            spread = 1.0f,
            smear = 1.0f,
            splash = 1.0f,
            edgeNoise = 1.0f,
            staining = 0.75f
        };

        if (profile == null)
        {
            return factors;
        }

        factors.absorption = profile.absorption;
        factors.roughness = profile.roughness;
        factors.spread = profile.spreadFactor;
        factors.smear = profile.smearFactor;
        factors.splash = profile.splashFactor;
        factors.edgeNoise = profile.edgeNoise;
        factors.staining = profile.colorStaining;

        return factors;
    }

    private Vector3 GetMainTangent(PaintImpactData impact, Vector3 normal)
    {
        Vector3 tangentVelocity = impact.TangentialVelocity;

        if (tangentVelocity.sqrMagnitude > 0.0001f)
        {
            return tangentVelocity.normalized;
        }

        Vector3 fallback = Vector3.ProjectOnPlane(Vector3.forward, normal);

        if (fallback.sqrMagnitude < 0.0001f)
        {
            fallback = Vector3.ProjectOnPlane(Vector3.right, normal);
        }

        return fallback.normalized;
    }

    private void DrawWetHaloIfNeeded(
        PaintImpactData impact,
        PaintImpactType impactType,
        SurfaceFactors factors,
        float baseRadius,
        float energy01,
        ref int generatedMarks
    )
    {
        if (!enableWetHalo)
        {
            return;
        }

        if (generatedMarks >= maxGeneratedMarksPerImpact)
        {
            return;
        }

        bool shouldCreateHalo =
            impactType == PaintImpactType.HeavyBlob ||
            impactType == PaintImpactType.HardSplash ||
            impactType == PaintImpactType.SkidImpact ||
            factors.absorption < 0.25f;

        if (!shouldCreateHalo)
        {
            return;
        }

        float haloRadius =
            baseRadius *
            wetHaloRadiusMultiplier *
            Mathf.Lerp(0.85f, 1.55f, energy01) *
            Mathf.Lerp(1.25f, 0.65f, factors.absorption);

        Color haloColor = Color.Lerp(impact.paintColor, wetHaloColor, 0.35f);
        haloColor.a = wetHaloOpacityMultiplier;

        canvasPainter.PaintImpactAtWorldPosition(
            impact.worldPosition,
            impact.incomingVelocity * 0.25f,
            haloColor,
            haloRadius
        );

        generatedMarks++;
    }

    private void DrawMainBlob(
        PaintImpactData impact,
        PaintImpactType impactType,
        SurfaceFactors factors,
        float baseRadius,
        Vector3 tangent,
        ref int generatedMarks
    )
    {
        if (generatedMarks >= maxGeneratedMarksPerImpact)
        {
            return;
        }

        float multiplier = baseBlobMultiplier;

        switch (impactType)
        {
            case PaintImpactType.SoftDeposit:
                multiplier *= 0.75f;
                break;

            case PaintImpactType.NormalSplat:
                multiplier *= 1.0f;
                break;

            case PaintImpactType.HardSplash:
                multiplier *= hardSplashMultiplier;
                break;

            case PaintImpactType.GrazingSmear:
                multiplier *= 0.85f;
                break;

            case PaintImpactType.MistImpact:
                multiplier *= mistMultiplier;
                break;

            case PaintImpactType.HeavyBlob:
                multiplier *= heavyBlobMultiplier;
                break;

            case PaintImpactType.SkidImpact:
                multiplier *= 0.8f;
                break;
        }

        multiplier *= Mathf.Lerp(0.75f, 1.45f, Mathf.Clamp01(factors.spread / 2f));
        multiplier *= Mathf.Lerp(0.75f, 1.15f, 1f - factors.absorption);

        Vector3 visualVelocity = impact.incomingVelocity;

        if (impactType == PaintImpactType.GrazingSmear || impactType == PaintImpactType.SkidImpact)
        {
            visualVelocity += tangent * impact.TangentialSpeed * 1.5f;
        }

        canvasPainter.PaintImpactAtWorldPosition(
            impact.worldPosition,
            visualVelocity,
            impact.paintColor,
            baseRadius * multiplier
        );

        generatedMarks++;
    }

    private void DrawDirectionalSmearIfNeeded(
        PaintImpactData impact,
        PaintImpactType impactType,
        SurfaceFactors factors,
        float baseRadius,
        Vector3 tangent,
        Vector3 bitangent,
        float tangentialSpeed,
        ref int generatedMarks
    )
    {
        bool shouldSmear =
            impactType == PaintImpactType.GrazingSmear ||
            impactType == PaintImpactType.SkidImpact ||
            tangentialSpeed > 1.7f;

        if (!shouldSmear)
        {
            return;
        }

        float smearLength = impactType == PaintImpactType.SkidImpact
            ? skidSmearLength
            : grazingSmearLength;

        smearLength *= Mathf.Lerp(0.7f, 1.8f, Mathf.Clamp01(factors.smear / 2f));
        smearLength *= Mathf.Lerp(0.65f, 1.35f, 1f - factors.absorption);

        for (int i = 1; i <= smearSteps; i++)
        {
            if (generatedMarks >= maxGeneratedMarksPerImpact)
            {
                return;
            }

            float t = i / (float)smearSteps;

            float sideNoise = Random.Range(-0.35f, 0.35f) * baseRadius * factors.roughness;
            Vector3 offset =
                tangent * baseRadius * smearLength * t +
                bitangent * sideNoise;

            float radius = baseRadius * Mathf.Pow(smearRadiusFalloff, i);
            radius = Mathf.Max(radius, baseRadius * 0.12f);

            Color smearColor = impact.paintColor;
            smearColor.a = Mathf.Lerp(0.55f, 0.12f, t);

            canvasPainter.PaintImpactAtWorldPosition(
                impact.worldPosition + offset,
                tangent * tangentialSpeed * Mathf.Lerp(1.4f, 0.35f, t),
                smearColor,
                radius
            );

            generatedMarks++;
        }
    }

    private void DrawSatelliteDroplets(
        PaintImpactData impact,
        PaintImpactType impactType,
        SurfaceFactors factors,
        float baseRadius,
        Vector3 tangent,
        Vector3 bitangent,
        float speed,
        float energy01,
        ref int generatedMarks
    )
    {
        int count = baseSatelliteCount;

        switch (impactType)
        {
            case PaintImpactType.HardSplash:
                count = hardSplashSatelliteCount;
                break;

            case PaintImpactType.MistImpact:
                count = mistSatelliteCount;
                break;

            case PaintImpactType.SoftDeposit:
                count = 1;
                break;

            case PaintImpactType.HeavyBlob:
                count = 3;
                break;

            case PaintImpactType.GrazingSmear:
            case PaintImpactType.SkidImpact:
                count = Mathf.RoundToInt(baseSatelliteCount * 1.4f);
                break;
        }

        count = Mathf.RoundToInt(count * Mathf.Lerp(0.4f, 1.8f, Mathf.Clamp01(factors.splash / 2f)));
        count = Mathf.RoundToInt(count * Mathf.Lerp(0.65f, 1.25f, energy01));

        for (int i = 0; i < count; i++)
        {
            if (generatedMarks >= maxGeneratedMarksPerImpact)
            {
                return;
            }

            Vector2 random = Random.insideUnitCircle;

            if (random.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            random.Normalize();

            float directionalBias = Mathf.Clamp01(impact.GrazingRatio01 + speed * 0.05f);
            Vector3 direction =
                Vector3.Slerp(
                    tangent,
                    tangent * Mathf.Sign(Random.Range(-1f, 1f)) + bitangent * random.y,
                    Random.Range(0.15f, 0.85f)
                ).normalized;

            if (Random.value > directionalBias)
            {
                direction = (tangent * random.x + bitangent * random.y).normalized;
            }

            float distance =
                baseRadius *
                Random.Range(satelliteMinDistance, satelliteMaxDistance) *
                Mathf.Lerp(0.75f, 1.6f, energy01) *
                Mathf.Lerp(0.75f, 1.35f, factors.splash);

            float radius =
                baseRadius *
                Random.Range(satelliteRadiusMin, satelliteRadiusMax);

            if (impactType == PaintImpactType.MistImpact)
            {
                radius *= 0.55f;
                distance *= 1.25f;
            }

            if (impactType == PaintImpactType.HeavyBlob)
            {
                radius *= 1.2f;
                distance *= 0.65f;
            }

            Vector3 point = impact.worldPosition + direction * distance;

            canvasPainter.PaintImpactAtWorldPosition(
                point,
                direction * speed * Random.Range(0.4f, 1.1f),
                impact.paintColor,
                radius
            );

            generatedMarks++;
        }
    }

    private void DrawFilamentsIfNeeded(
        PaintImpactData impact,
        PaintImpactType impactType,
        SurfaceFactors factors,
        float baseRadius,
        Vector3 tangent,
        Vector3 bitangent,
        float energy01,
        ref int generatedMarks
    )
    {
        bool shouldCreateFilaments =
            impactType == PaintImpactType.HardSplash ||
            impactType == PaintImpactType.GrazingSmear ||
            impactType == PaintImpactType.SkidImpact ||
            (impact.viscosity < 0.8f && impact.ImpactSpeed > 2.0f);

        if (!shouldCreateFilaments)
        {
            return;
        }

        int count = impactType == PaintImpactType.HardSplash
            ? hardSplashFilamentCount
            : baseFilamentCount;

        count = Mathf.RoundToInt(count * Mathf.Lerp(0.5f, 1.4f, energy01));
        count = Mathf.Max(count, 1);

        for (int i = 0; i < count; i++)
        {
            if (generatedMarks >= maxGeneratedMarksPerImpact)
            {
                return;
            }

            float side = Random.Range(-1f, 1f);
            Vector3 direction = (tangent + bitangent * side * Random.Range(0.15f, 0.85f)).normalized;

            float length =
                baseRadius *
                Random.Range(filamentLengthMin, filamentLengthMax) *
                Mathf.Lerp(0.8f, 1.6f, energy01) *
                Mathf.Lerp(0.8f, 1.4f, factors.smear);

            int steps = Random.Range(2, 5);

            for (int s = 1; s <= steps; s++)
            {
                if (generatedMarks >= maxGeneratedMarksPerImpact)
                {
                    return;
                }

                float t = s / (float)steps;

                Vector3 point =
                    impact.worldPosition +
                    direction * length * t +
                    bitangent * Random.Range(-0.18f, 0.18f) * baseRadius * factors.roughness;

                float radius =
                    baseRadius *
                    filamentRadiusMultiplier *
                    Mathf.Lerp(1f, 0.25f, t);

                Color color = impact.paintColor;
                color.a = Mathf.Lerp(0.45f, 0.08f, t);

                canvasPainter.PaintImpactAtWorldPosition(
                    point,
                    direction * impact.ImpactSpeed * 0.6f,
                    color,
                    radius
                );

                generatedMarks++;
            }
        }
    }
}