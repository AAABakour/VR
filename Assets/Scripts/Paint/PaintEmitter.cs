using UnityEngine;

public class PaintEmitter : MonoBehaviour
{
    public enum NozzleShape
    {
        Circular,
        Slit,
        DoubleHole,
        Irregular
    }

    [Header("References")]
    public CanvasPainter canvasPainter;
    public PaintParticleSimulator particleSimulator;

    [Header("Paint Properties")]
    public Color paintColor = Color.red;
    public float initialPaintAmount = 5f;
    public float holeDiameter = 0.04f;
    public float viscosity = 1f;

    [Header("Nozzle Shape")]
    public NozzleShape nozzleShape = NozzleShape.Circular;
    public float nozzleSpreadRadius = 0.025f;
    public float slitWidth = 0.12f;
    public float doubleHoleSpacing = 0.08f;
    [Range(0f, 1f)]
    public float irregularNozzleNoise = 0.55f;

    [Header("Flow Settings")]
    public float baseFlowRate = 0.35f;
    public float referenceHoleDiameter = 0.04f;
    [Tooltip("Keeps the bucket draining like a real open hole instead of asymptotically slowing forever.")]
    public bool finiteDrainMode = true;
    [Range(0f, 0.6f)]
    public float minimumFlowFractionWhileNotEmpty = 0.16f;
    public float snapEmptyBelowAmount = 0.015f;

    [Header("Particle Emission Settings")]
    public float particlesPerUnitFlow = 90f;
    public float downwardStartSpeed = 1.8f;
    public float nozzleVelocityInfluence = 0.85f;
    public float randomSpread = 0.15f;

    [Header("Performance Safety")]
    [Tooltip("Prevents one slow frame or an extreme UI value from spawning a huge burst that freezes the project.")]
    public bool clampParticleBurstPerFrame = true;
    [Range(16, 600)] public int maxParticlesEmittedPerFrame = 160;
    [Range(0f, 600f)] public float maxEmissionBacklog = 80f;

    [Header("Particle Size")]
    public float minParticleRadius = 0.018f;
    public float maxParticleRadius = 0.055f;

    [Header("Internal Liquid Sloshing")]
    public bool enableInternalSlosh = true;
    public float sloshStrength = 0.012f;
    public float maxSloshOffset = 0.08f;
    public float sloshResponseTime = 0.18f;

    [Range(0f, 2f)]
    public float sloshFlowInfluence = 0.65f;

    [Range(0f, 2f)]
    public float sloshDirectionInfluence = 0.55f;

    [Range(0f, 1f)]
    public float flowNoiseAmount = 0.22f;

    public float flowNoiseSpeed = 4f;

    private float remainingPaintAmount;
    private float emissionAccumulator;
    private float currentFlowRate;
    private int lastEmittedParticleCount;

    private Vector3 previousNozzlePosition;
    private Vector3 previousNozzleVelocity;
    private bool hasPreviousNozzlePosition;

    private Vector3 sloshOffset;
    private Vector3 sloshSmoothVelocity;

    void Start()
    {
        remainingPaintAmount = initialPaintAmount;

        if (particleSimulator == null)
        {
            particleSimulator = GetComponent<PaintParticleSimulator>();
        }

        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }
    }

    void Update()
    {
        if (remainingPaintAmount <= 0f)
        {
            currentFlowRate = 0f;
            lastEmittedParticleCount = 0;
            return;
        }

        if (particleSimulator == null)
        {
            currentFlowRate = 0f;
            lastEmittedParticleCount = 0;
            return;
        }

        float dt = Time.deltaTime;

        Vector3 nozzlePosition = transform.position;
        Vector3 nozzleVelocity = CalculateNozzleVelocity(nozzlePosition, dt);
        Vector3 nozzleAcceleration = CalculateNozzleAcceleration(nozzleVelocity, dt);

        UpdateInternalSlosh(nozzleAcceleration, dt);

        float flowRate = CalculateFlowRate();
        currentFlowRate = flowRate;

        remainingPaintAmount -= flowRate * dt;
        if (finiteDrainMode && remainingPaintAmount <= snapEmptyBelowAmount)
        {
            remainingPaintAmount = 0f;
        }
        remainingPaintAmount = Mathf.Max(remainingPaintAmount, 0f);

        emissionAccumulator += flowRate * particlesPerUnitFlow * dt;

        if (clampParticleBurstPerFrame)
        {
            emissionAccumulator = Mathf.Min(emissionAccumulator, Mathf.Max(maxEmissionBacklog, maxParticlesEmittedPerFrame));
        }

        int desiredParticlesToEmit = Mathf.FloorToInt(emissionAccumulator);
        int particlesToEmit = desiredParticlesToEmit;

        if (clampParticleBurstPerFrame)
        {
            particlesToEmit = Mathf.Min(particlesToEmit, Mathf.Max(1, maxParticlesEmittedPerFrame));
        }

        emissionAccumulator -= particlesToEmit;

        if (clampParticleBurstPerFrame && desiredParticlesToEmit > particlesToEmit)
        {
            emissionAccumulator = Mathf.Min(emissionAccumulator, Mathf.Max(0f, maxEmissionBacklog));
        }

        lastEmittedParticleCount = particlesToEmit;

        for (int i = 0; i < particlesToEmit; i++)
        {
            EmitOneParticle(nozzlePosition, nozzleVelocity, flowRate);
        }
    }

    private Vector3 CalculateNozzleVelocity(Vector3 currentPosition, float dt)
    {
        if (!hasPreviousNozzlePosition || dt <= 0f)
        {
            previousNozzlePosition = currentPosition;
            hasPreviousNozzlePosition = true;
            return Vector3.zero;
        }

        Vector3 velocity = (currentPosition - previousNozzlePosition) / dt;
        previousNozzlePosition = currentPosition;

        return velocity;
    }

    private Vector3 CalculateNozzleAcceleration(Vector3 currentVelocity, float dt)
    {
        if (dt <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 acceleration = (currentVelocity - previousNozzleVelocity) / dt;
        previousNozzleVelocity = currentVelocity;

        return acceleration;
    }

    private void UpdateInternalSlosh(Vector3 nozzleAcceleration, float dt)
    {
        if (!enableInternalSlosh)
        {
            sloshOffset = Vector3.zero;
            return;
        }

        Vector3 horizontalAcceleration = new Vector3(
            nozzleAcceleration.x,
            0f,
            nozzleAcceleration.z
        );

        Vector3 targetSloshOffset = -horizontalAcceleration * sloshStrength;
        targetSloshOffset = Vector3.ClampMagnitude(targetSloshOffset, maxSloshOffset);

        sloshOffset = Vector3.SmoothDamp(
            sloshOffset,
            targetSloshOffset,
            ref sloshSmoothVelocity,
            Mathf.Max(sloshResponseTime, 0.01f),
            Mathf.Infinity,
            dt
        );
    }

    private float CalculateFlowRate()
    {
        float safeViscosity = Mathf.Max(viscosity, 0.1f);

        float holeFactor = holeDiameter / referenceHoleDiameter;
        holeFactor *= holeFactor;

        float fill01 = initialPaintAmount > 0f ? Mathf.Clamp01(remainingPaintAmount / initialPaintAmount) : 0f;
        float amountFactor = finiteDrainMode
            ? Mathf.Lerp(Mathf.Clamp01(minimumFlowFractionWhileNotEmpty), 1f, Mathf.Pow(fill01, 0.35f))
            : fill01;

        float shapeFactor = GetNozzleShapeFlowMultiplier();

        float sloshIntensity = 0f;

        if (enableInternalSlosh && maxSloshOffset > 0f)
        {
            sloshIntensity = Mathf.Clamp01(sloshOffset.magnitude / maxSloshOffset);
        }

        float flowNoise = Mathf.PerlinNoise(Time.time * flowNoiseSpeed, remainingPaintAmount * 0.13f);
        flowNoise = (flowNoise - 0.5f) * 2f;

        float irregularFlowFactor =
            1f
            + sloshIntensity * sloshFlowInfluence
            + flowNoise * flowNoiseAmount;

        irregularFlowFactor = Mathf.Clamp(irregularFlowFactor, 0.2f, 2.2f);

        float flowRate =
            baseFlowRate
            * holeFactor
            * shapeFactor
            * amountFactor
            * irregularFlowFactor
            / safeViscosity;

        return Mathf.Max(flowRate, 0f);
    }

    private float GetNozzleShapeFlowMultiplier()
    {
        switch (nozzleShape)
        {
            case NozzleShape.Circular:
                return 1f;

            case NozzleShape.Slit:
                return 1.15f;

            case NozzleShape.DoubleHole:
                return 1.35f;

            case NozzleShape.Irregular:
                return 0.9f + Random.Range(-0.12f, 0.18f);
        }

        return 1f;
    }

    private void EmitOneParticle(Vector3 nozzlePosition, Vector3 nozzleVelocity, float flowRate)
    {
        float safeViscosity = Mathf.Max(viscosity, 0.2f);

        Vector3 localOffset = GetNozzleLocalOffset();
        Vector3 spawnPosition = nozzlePosition + transform.TransformDirection(localOffset);

        float sloshIntensity = 0f;
        Vector3 sloshDirection = Vector3.zero;

        if (enableInternalSlosh && maxSloshOffset > 0f)
        {
            sloshIntensity = Mathf.Clamp01(sloshOffset.magnitude / maxSloshOffset);

            if (sloshOffset.sqrMagnitude > 0.0001f)
            {
                sloshDirection = sloshOffset.normalized;
            }
        }

        Vector3 randomHorizontalSpread = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        );

        randomHorizontalSpread *= randomSpread * (1f + sloshIntensity * 1.4f) / safeViscosity;

        Vector3 shapeKick = transform.TransformDirection(localOffset.normalized) * 0.25f;

        Vector3 sloshKick =
            sloshDirection
            * sloshIntensity
            * sloshDirectionInfluence;

        Vector3 particleVelocity =
            nozzleVelocity * nozzleVelocityInfluence
            + Vector3.down * downwardStartSpeed
            + randomHorizontalSpread
            + shapeKick
            + sloshKick;

        float flow01 = Mathf.Clamp01(flowRate / Mathf.Max(baseFlowRate, 0.001f));

        float radius = Mathf.Lerp(minParticleRadius, maxParticleRadius, flow01);
        radius *= Random.Range(0.75f, 1.25f);

        if (nozzleShape == NozzleShape.Slit)
        {
            radius *= Random.Range(0.75f, 1.05f);
        }

        if (nozzleShape == NozzleShape.DoubleHole)
        {
            radius *= Random.Range(0.85f, 1.1f);
        }

        if (nozzleShape == NozzleShape.Irregular)
        {
            radius *= Random.Range(0.6f, 1.35f);
        }

        particleSimulator.EmitParticle(
            spawnPosition,
            particleVelocity,
            paintColor,
            radius,
            viscosity
        );
    }

    private Vector3 GetNozzleLocalOffset()
    {
        switch (nozzleShape)
        {
            case NozzleShape.Circular:
                return GetCircularOffset();

            case NozzleShape.Slit:
                return GetSlitOffset();

            case NozzleShape.DoubleHole:
                return GetDoubleHoleOffset();

            case NozzleShape.Irregular:
                return GetIrregularOffset();
        }

        return Vector3.zero;
    }

    private Vector3 GetCircularOffset()
    {
        Vector2 offset = Random.insideUnitCircle * nozzleSpreadRadius;
        return new Vector3(offset.x, 0f, offset.y);
    }

    private Vector3 GetSlitOffset()
    {
        float x = Random.Range(-slitWidth * 0.5f, slitWidth * 0.5f);
        float z = Random.Range(-nozzleSpreadRadius * 0.25f, nozzleSpreadRadius * 0.25f);

        return new Vector3(x, 0f, z);
    }

    private Vector3 GetDoubleHoleOffset()
    {
        float side = Random.value < 0.5f ? -1f : 1f;

        Vector2 smallRandom = Random.insideUnitCircle * nozzleSpreadRadius * 0.45f;

        return new Vector3(
            side * doubleHoleSpacing * 0.5f + smallRandom.x,
            0f,
            smallRandom.y
        );
    }

    private Vector3 GetIrregularOffset()
    {
        Vector2 offset = Random.insideUnitCircle * nozzleSpreadRadius;

        float noise = Mathf.PerlinNoise(
            Time.time * 8f + offset.x * 20f,
            Time.time * 5f + offset.y * 20f
        );

        float scale = Mathf.Lerp(0.4f, 1.8f, noise * irregularNozzleNoise);

        offset *= scale;

        return new Vector3(offset.x, 0f, offset.y);
    }

    public void ApplyPerformanceBudget(float flowRate, float particlesPerFlow, int burstLimit, float backlogLimit)
    {
        baseFlowRate = Mathf.Clamp(flowRate, 0f, 1.25f);
        particlesPerUnitFlow = Mathf.Clamp(particlesPerFlow, 5f, 240f);
        maxParticlesEmittedPerFrame = Mathf.Clamp(burstLimit, 16, 600);
        maxEmissionBacklog = Mathf.Clamp(backlogLimit, 0f, 600f);
        clampParticleBurstPerFrame = true;
        emissionAccumulator = Mathf.Min(emissionAccumulator, maxEmissionBacklog);
    }

    public void ResetEmitter()
    {
        remainingPaintAmount = initialPaintAmount;
        emissionAccumulator = 0f;
        currentFlowRate = 0f;
        lastEmittedParticleCount = 0;

        hasPreviousNozzlePosition = false;
        previousNozzleVelocity = Vector3.zero;

        sloshOffset = Vector3.zero;
        sloshSmoothVelocity = Vector3.zero;

        if (particleSimulator != null)
        {
            particleSimulator.ResetParticles();
        }
    }

    public float RemainingPaintAmount
    {
        get
        {
            return remainingPaintAmount;
        }
    }

    public float PaintFill01
    {
        get
        {
            if (initialPaintAmount <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(remainingPaintAmount / initialPaintAmount);
        }
    }



    public void RefillToInitialAmount()
    {
        remainingPaintAmount = Mathf.Max(0f, initialPaintAmount);
        emissionAccumulator = 0f;
        currentFlowRate = 0f;
        lastEmittedParticleCount = 0;
    }

    public void SetRemainingPaintAmount(float amount)
    {
        remainingPaintAmount = Mathf.Clamp(amount, 0f, Mathf.Max(initialPaintAmount, 0.0001f));
        if (remainingPaintAmount <= 0f)
        {
            emissionAccumulator = 0f;
            currentFlowRate = 0f;
            lastEmittedParticleCount = 0;
        }
    }

    public void SetFill01(float normalizedFill)
    {
        normalizedFill = Mathf.Clamp01(normalizedFill);
        remainingPaintAmount = Mathf.Max(0f, initialPaintAmount) * normalizedFill;
        emissionAccumulator = 0f;
        if (remainingPaintAmount <= 0f)
        {
            currentFlowRate = 0f;
            lastEmittedParticleCount = 0;
        }
    }

    public void PreserveFillWhenChangingCapacity(float newInitialAmount)
    {
        float oldFill = PaintFill01;
        initialPaintAmount = Mathf.Max(0.0001f, newInitialAmount);
        remainingPaintAmount = initialPaintAmount * oldFill;
    }

    public float CurrentFlowRate
    {
        get { return currentFlowRate; }
    }

    public int LastEmittedParticleCount
    {
        get { return lastEmittedParticleCount; }
    }

    public bool IsPaintEmpty
    {
        get { return remainingPaintAmount <= 0.0001f; }
    }

    public Vector3 SloshOffset
    {
        get { return sloshOffset; }
    }

    public float SloshIntensity01
    {
        get
        {
            if (!enableInternalSlosh || maxSloshOffset <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(sloshOffset.magnitude / maxSloshOffset);
        }
    }

    public string CurrentNozzleShapeName
    {
        get
        {
            return nozzleShape.ToString();
        }
    }
}