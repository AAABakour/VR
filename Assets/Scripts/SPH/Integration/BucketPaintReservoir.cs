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
    public bool allowInfiniteDebugEmission = false;
    public bool resetOnPlay = true;

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
        get { return !allowInfiniteDebugEmission && remainingPaintAmount <= minEmissionThreshold; }
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
    }

    private void SetRemainingPaint(float amount)
    {
        remainingPaintAmount = Mathf.Clamp(amount, 0f, maxPaintAmount);
        fillPercent = FillPercent;
        isEmpty = IsEmpty;
    }
}
