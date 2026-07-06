using UnityEngine;

[System.Serializable]
public struct PaintImpactData
{
    public Vector3 worldPosition;
    public Vector3 surfaceNormal;
    public Vector3 incomingVelocity;

    public Color paintColor;

    public float particleRadius;
    public float particleMass;
    public float viscosity;
    public float wetness;

    public int sourceId;

    public float ImpactSpeed
    {
        get
        {
            return incomingVelocity.magnitude;
        }
    }

    public Vector3 SafeNormal
    {
        get
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                return Vector3.up;
            }

            return surfaceNormal.normalized;
        }
    }

    public float NormalSpeed
    {
        get
        {
            return Mathf.Abs(Vector3.Dot(incomingVelocity, SafeNormal));
        }
    }

    public Vector3 TangentialVelocity
    {
        get
        {
            Vector3 normal = SafeNormal;
            return incomingVelocity - Vector3.Dot(incomingVelocity, normal) * normal;
        }
    }

    public float TangentialSpeed
    {
        get
        {
            return TangentialVelocity.magnitude;
        }
    }

    public float ImpactEnergy
    {
        get
        {
            return 0.5f * Mathf.Max(particleMass, 0.0001f) * ImpactSpeed * ImpactSpeed;
        }
    }

    public float GrazingRatio01
    {
        get
        {
            float total = ImpactSpeed;

            if (total <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(TangentialSpeed / total);
        }
    }
}