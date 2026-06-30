using UnityEngine;

[DefaultExecutionOrder(80)]
public class BucketPaintReservoir : MonoBehaviour
{
    [Header("Paint Amount")]
    [Min(0.001f)]
    public float maxPaintAmount = 5f;
    [Min(0f)]
    public float initialPaintAmount = 5f;
    [Min(0f)]
    public float remainingPaintAmount = 5f;
    [Range(0f, 1f)]
    public float fillPercent = 1f;
    public bool isEmpty;

    [Header("Drain")]
    [Min(0.000001f)]
    public float paintAmountPerParticle = 0.001f;
    [Min(0f)]
    public float drainRateMultiplier = 1f;
    [Min(0f)]
    public float minEmissionThreshold = 0.001f;
    [Min(0.001f)]
    public float referenceNozzleRadius = 0.024f;
    [Min(0.01f)]
    public float viscosityFactor = 1f;
    [Range(0f, 1f)]
    public float minFlowWhenNotEmpty = 0.08f;
    public bool stopAtEmpty = true;
    public bool allowInfiniteDebugEmission = false;
    public bool resetOnPlay = true;

    public float currentFlowFactor = 1f;
    public float estimatedSecondsRemaining = 0f;

    public float RemainingPaintAmount
    {
        get { return remainingPaintAmount; }
    }

    public float FillPercent
    {
        get
        {
            if (maxPaintAmount <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(remainingPaintAmount / maxPaintAmount);
        }
    }

    public bool IsEmpty
    {
        get { return stopAtEmpty && !allowInfiniteDebugEmission && remainingPaintAmount <= minEmissionThreshold; }
    }

    private void Awake()
    {
        ClampSerializedValues();
        if (resetOnPlay)
        {
            ResetReservoir();
        }
        else
        {
            SetRemainingPaint(remainingPaintAmount);
        }
    }

    private void OnValidate()
    {
        ClampSerializedValues();
        SetRemainingPaint(Application.isPlaying ? remainingPaintAmount : initialPaintAmount);
    }

    public void ResetReservoir()
    {
        SetRemainingPaint(initialPaintAmount);
    }

    public bool CanEmit(int requestedParticles)
    {
        if (requestedParticles <= 0)
        {
            return false;
        }

        if (allowInfiniteDebugEmission)
        {
            return true;
        }

        return remainingPaintAmount > minEmissionThreshold &&
            ClampEmissionToAvailablePaint(requestedParticles) > 0;
    }

    public int ClampEmissionToAvailablePaint(int requestedParticles)
    {
        if (requestedParticles <= 0)
        {
            return 0;
        }

        if (allowInfiniteDebugEmission)
        {
            return requestedParticles;
        }

        float usablePaint = Mathf.Max(0f, remainingPaintAmount - minEmissionThreshold);
        if (usablePaint <= 0f)
        {
            return 0;
        }

        int availableParticles = Mathf.FloorToInt(usablePaint / Mathf.Max(0.000001f, paintAmountPerParticle));
        return Mathf.Clamp(requestedParticles, 0, availableParticles);
    }

    public float ConsumeForParticles(int particleCount)
    {
        if (particleCount <= 0)
        {
            return 0f;
        }

        float consumed = particleCount * Mathf.Max(0.000001f, paintAmountPerParticle);
        if (allowInfiniteDebugEmission)
        {
            return consumed;
        }

        float clampedConsumed = Mathf.Min(consumed, remainingPaintAmount);
        SetRemainingPaint(remainingPaintAmount - clampedConsumed);
        return clampedConsumed;
    }

    public float CalculateFlowFactor(float nozzleRadius)
    {
        if (allowInfiniteDebugEmission)
        {
            currentFlowFactor = Mathf.Max(0f, drainRateMultiplier);
            estimatedSecondsRemaining = float.PositiveInfinity;
            return currentFlowFactor;
        }

        if (IsEmpty)
        {
            currentFlowFactor = 0f;
            estimatedSecondsRemaining = 0f;
            return 0f;
        }

        float fill = Mathf.Clamp01(FillPercent);
        float fillFlow = Mathf.Sqrt(Mathf.Max(fill, 0.0001f));
        fillFlow = Mathf.Max(minFlowWhenNotEmpty, fillFlow);

        float radiusRatio = Mathf.Max(0.001f, nozzleRadius) / Mathf.Max(0.001f, referenceNozzleRadius);
        float holeFactor = Mathf.Clamp(radiusRatio * radiusRatio, 0.15f, 3f);
        float viscosity = 1f / Mathf.Max(0.01f, viscosityFactor);

        currentFlowFactor = fillFlow * holeFactor * viscosity * drainRateMultiplier;
        return currentFlowFactor;
    }

    public float EstimateSecondsRemaining(float particlesPerSecond, float nozzleRadius)
    {
        if (allowInfiniteDebugEmission)
        {
            estimatedSecondsRemaining = float.PositiveInfinity;
            return estimatedSecondsRemaining;
        }

        float flow = CalculateFlowFactor(nozzleRadius);
        float paintPerSecond = particlesPerSecond * flow * paintAmountPerParticle;
        estimatedSecondsRemaining = paintPerSecond > 0.000001f
            ? Mathf.Max(0f, remainingPaintAmount - minEmissionThreshold) / paintPerSecond
            : 0f;
        return estimatedSecondsRemaining;
    }

    public void AddPaint(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetRemainingPaint(remainingPaintAmount + amount);
    }

    private void ClampSerializedValues()
    {
        maxPaintAmount = Mathf.Max(0.001f, maxPaintAmount);
        initialPaintAmount = Mathf.Clamp(initialPaintAmount, 0f, maxPaintAmount);
        paintAmountPerParticle = Mathf.Max(0.000001f, paintAmountPerParticle);
        minEmissionThreshold = Mathf.Max(0f, minEmissionThreshold);
        drainRateMultiplier = Mathf.Max(0f, drainRateMultiplier);
        referenceNozzleRadius = Mathf.Max(0.001f, referenceNozzleRadius);
        viscosityFactor = Mathf.Max(0.01f, viscosityFactor);
    }

    private void SetRemainingPaint(float amount)
    {
        remainingPaintAmount = Mathf.Clamp(amount, 0f, maxPaintAmount);
        fillPercent = FillPercent;
        isEmpty = IsEmpty;
        if (isEmpty)
        {
            estimatedSecondsRemaining = 0f;
        }
    }
}
