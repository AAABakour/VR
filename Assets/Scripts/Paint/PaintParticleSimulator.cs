using System.Collections.Generic;
using UnityEngine;

public class PaintParticleSimulator : MonoBehaviour
{
    [Header("References")]
    public CanvasPainter canvasPainter;
    public PaintImpactEngineV2 impactEngine;

    [Header("Particle Motion")]
    public float gravity = 9.81f;
    public float airDrag = 0.25f;
    public int maxParticles = 1200;

    [Header("Wind / Air Turbulence")]
    public bool enableAirTurbulence = true;
    public Vector3 windDirection = new Vector3(0.35f, 0f, 0.15f);
    public float windStrength = 0.16f;
    public float turbulenceStrength = 0.28f;
    public float turbulenceFrequency = 1.4f;
    public float turbulenceVerticalInfluence = 0.08f;

    [Range(0f, 1f)]
    public float viscosityAirResistance = 0.55f;

    [Header("Optimized Visual Droplets")]
    public bool showVisualDroplets = true;
    public GameObject particleVisualPrefab;
    public Transform visualParent;
    public int visualPoolSize = 120;
    public float visualScaleMultiplier = 1.2f;
    public float minVisualScale = 0.025f;
    public float maxVisualScale = 0.08f;

    [Header("Particle Interaction")]
    public bool enableParticleInteraction = true;
    public float interactionRadius = 0.16f;
    public float cohesionStrength = 0.14f;
    public float separationStrength = 0.07f;
    public float viscosityAlignment = 0.09f;
    public int maxInteractionChecks = 16;

    [Header("Runtime Performance Guardrails")]
    public bool autoCullWhenOverBudget = true;
    [Range(16, 1200)] public int maxParticlesSimulatedPerFrame = 1200;

    [Header("Impact Settings")]
    public float impactRadiusMultiplier = 1.2f;
    public float minImpactRadius = 0.015f;
    public float maxImpactRadius = 0.10f;

    [Header("Surface Collision V2")]
    public bool useExactCanvasPlaneCollision = true;
    public bool rejectImpactsOutsideCanvas = true;
    public float canvasBoundsPadding = 0.025f;
    public bool enableLegacySecondarySpray = false;

    private struct PaintParticle
    {
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;
        public Color color;
        public float radius;
        public float viscosity;
        public float lifeTime;
        public int visualIndex;
    }

    private class DropletVisual
    {
        public GameObject gameObject;
        public Transform transform;
        public Renderer renderer;
        public MaterialPropertyBlock propertyBlock;
        public bool isActive;
    }

    private readonly List<PaintParticle> particles = new List<PaintParticle>();
    private DropletVisual[] visualPool;
    private int nextVisualSearchIndex;

    void Awake()
    {
        if (canvasPainter == null)
        {
            canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        }

        if (visualParent == null)
        {
            visualParent = transform;
        }

        if (impactEngine == null)
        {
            impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        }

        InitializeVisualPool();
    }

    private void InitializeVisualPool()
    {
        if (!showVisualDroplets || particleVisualPrefab == null || visualPoolSize <= 0)
        {
            visualPool = new DropletVisual[0];
            return;
        }

        visualPool = new DropletVisual[visualPoolSize];

        for (int i = 0; i < visualPoolSize; i++)
        {
            GameObject visualObject = Instantiate(
                particleVisualPrefab,
                visualParent
            );

            visualObject.name = "PaintDroplet_Pooled";
            visualObject.SetActive(false);

            DropletVisual visual = new DropletVisual
            {
                gameObject = visualObject,
                transform = visualObject.transform,
                renderer = visualObject.GetComponent<Renderer>(),
                propertyBlock = new MaterialPropertyBlock(),
                isActive = false
            };

            visualPool[i] = visual;
        }
    }

    void Update()
    {
        if (canvasPainter == null)
        {
            return;
        }

        float dt = Time.deltaTime;

        if (autoCullWhenOverBudget)
        {
            CullToParticleBudget();
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            PaintParticle particle = particles[i];

            particle.previousPosition = particle.position;

            if (enableParticleInteraction)
            {
                ApplyParticleInteraction(ref particle, i, dt);
            }

            if (enableAirTurbulence)
            {
                ApplyAirTurbulence(ref particle, dt);
            }

            particle.velocity += Vector3.down * gravity * dt;

            float viscosityDrag = airDrag / Mathf.Max(particle.viscosity, 0.2f);
            particle.velocity *= Mathf.Clamp01(1f - viscosityDrag * dt);

            particle.position += particle.velocity * dt;
            particle.lifeTime -= dt;

            UpdateParticleVisual(particle);

            if (TryResolveCanvasImpact(
                particle.previousPosition,
                particle.position,
                out Vector3 hitPoint,
                out Vector3 hitNormal,
                out bool crossedCanvasPlane
            ))
            {
                PaintImpact(hitPoint, hitNormal, particle);
                RemoveParticleAt(i);
                continue;
            }

            if (crossedCanvasPlane && rejectImpactsOutsideCanvas)
            {
                RemoveParticleAt(i);
                continue;
            }

            if (particle.lifeTime <= 0f)
            {
                RemoveParticleAt(i);
                continue;
            }

            particles[i] = particle;
        }
    }

    public void EmitParticle(
        Vector3 position,
        Vector3 velocity,
        Color color,
        float radius,
        float viscosity
    )
    {
        if (particles.Count >= maxParticles)
        {
            RemoveParticleAt(0);
        }

        int visualIndex = ActivateVisual(position, color, radius);

        PaintParticle particle = new PaintParticle
        {
            position = position,
            previousPosition = position,
            velocity = velocity,
            color = color,
            radius = radius,
            viscosity = viscosity,
            lifeTime = 4f,
            visualIndex = visualIndex
        };

        particles.Add(particle);
    }

    private int ActivateVisual(Vector3 position, Color color, float radius)
    {
        if (!showVisualDroplets || visualPool == null || visualPool.Length == 0)
        {
            return -1;
        }

        for (int attempt = 0; attempt < visualPool.Length; attempt++)
        {
            int index = (nextVisualSearchIndex + attempt) % visualPool.Length;
            DropletVisual visual = visualPool[index];

            if (visual.isActive)
            {
                continue;
            }

            visual.isActive = true;
            visual.gameObject.SetActive(true);
            visual.transform.position = position;
            visual.transform.rotation = Quaternion.identity;

            float scale = Mathf.Clamp(
                radius * visualScaleMultiplier,
                minVisualScale,
                maxVisualScale
            );

            visual.transform.localScale = Vector3.one * scale;

            ApplyVisualColor(visual, color);

            nextVisualSearchIndex = (index + 1) % visualPool.Length;

            return index;
        }

        return -1;
    }

    private void ApplyVisualColor(DropletVisual visual, Color color)
    {
        if (visual.renderer == null)
        {
            return;
        }

        visual.renderer.GetPropertyBlock(visual.propertyBlock);

        visual.propertyBlock.SetColor("_BaseColor", color);
        visual.propertyBlock.SetColor("_Color", color);

        visual.renderer.SetPropertyBlock(visual.propertyBlock);
    }

    private void UpdateParticleVisual(PaintParticle particle)
    {
        if (particle.visualIndex < 0)
        {
            return;
        }

        if (visualPool == null || particle.visualIndex >= visualPool.Length)
        {
            return;
        }

        DropletVisual visual = visualPool[particle.visualIndex];

        if (!visual.isActive)
        {
            return;
        }

        visual.transform.position = particle.position;

        if (particle.velocity.sqrMagnitude > 0.001f)
        {
            visual.transform.rotation = Quaternion.LookRotation(particle.velocity.normalized);
        }
    }

    private void ReleaseVisual(int visualIndex)
    {
        if (visualIndex < 0 || visualPool == null || visualIndex >= visualPool.Length)
        {
            return;
        }

        DropletVisual visual = visualPool[visualIndex];

        visual.isActive = false;
        visual.gameObject.SetActive(false);
    }

    private void ApplyAirTurbulence(ref PaintParticle particle, float dt)
    {
        float viscosityResponse = 1f / Mathf.Max(particle.viscosity, 0.2f);
        viscosityResponse = Mathf.Clamp(viscosityResponse, 0.35f, 1.8f);

        float airResponse = Mathf.Lerp(1f, viscosityResponse, viscosityAirResistance);

        Vector3 wind = Vector3.zero;

        if (windDirection.sqrMagnitude > 0.0001f)
        {
            wind = windDirection.normalized * windStrength * airResponse;
        }

        float time = Time.time * turbulenceFrequency;

        float noiseX = Mathf.PerlinNoise(
            particle.position.x * 0.65f + time,
            particle.position.y * 0.35f
        );

        float noiseZ = Mathf.PerlinNoise(
            particle.position.z * 0.65f + time + 12.37f,
            particle.position.x * 0.35f
        );

        float noiseY = Mathf.PerlinNoise(
            particle.position.y * 0.65f + time + 25.91f,
            particle.position.z * 0.35f
        );

        noiseX = noiseX * 2f - 1f;
        noiseZ = noiseZ * 2f - 1f;
        noiseY = noiseY * 2f - 1f;

        Vector3 turbulence = new Vector3(
            noiseX,
            noiseY * turbulenceVerticalInfluence,
            noiseZ
        );

        turbulence *= turbulenceStrength * airResponse;

        particle.velocity += (wind + turbulence) * dt;
    }

    private void ApplyParticleInteraction(ref PaintParticle particle, int particleIndex, float dt)
    {
        if (particles.Count <= 1)
        {
            return;
        }

        float radiusSquared = interactionRadius * interactionRadius;
        float separationRadius = interactionRadius * 0.42f;

        int startIndex = Mathf.Max(0, particleIndex - maxInteractionChecks);
        int endIndex = Mathf.Min(particles.Count - 1, particleIndex + maxInteractionChecks);

        Vector3 cohesionForce = Vector3.zero;
        Vector3 separationForce = Vector3.zero;
        Vector3 averageHorizontalVelocity = Vector3.zero;

        int neighborCount = 0;

        for (int j = startIndex; j <= endIndex; j++)
        {
            if (j == particleIndex)
            {
                continue;
            }

            PaintParticle other = particles[j];

            Vector3 difference = other.position - particle.position;

            Vector3 horizontalDifference = new Vector3(
                difference.x,
                0f,
                difference.z
            );

            float sqrDistance = horizontalDifference.sqrMagnitude;

            if (sqrDistance <= 0.000001f || sqrDistance > radiusSquared)
            {
                continue;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            Vector3 direction = horizontalDifference / distance;

            float closeness = 1f - distance / interactionRadius;

            cohesionForce += direction * closeness;

            if (distance < separationRadius)
            {
                float push = 1f - distance / separationRadius;
                separationForce -= direction * push;
            }

            averageHorizontalVelocity += new Vector3(
                other.velocity.x,
                0f,
                other.velocity.z
            );

            neighborCount++;
        }

        if (neighborCount == 0)
        {
            return;
        }

        cohesionForce /= neighborCount;
        separationForce /= neighborCount;
        averageHorizontalVelocity /= neighborCount;

        float viscosityFactor = Mathf.Clamp01(1f / Mathf.Max(particle.viscosity, 0.2f));

        particle.velocity += cohesionForce * cohesionStrength * dt;
        particle.velocity += separationForce * separationStrength * dt;

        Vector3 currentHorizontalVelocity = new Vector3(
            particle.velocity.x,
            0f,
            particle.velocity.z
        );

        Vector3 smoothedHorizontalVelocity = Vector3.Lerp(
            currentHorizontalVelocity,
            averageHorizontalVelocity,
            viscosityAlignment * viscosityFactor * dt
        );

        particle.velocity = new Vector3(
            smoothedHorizontalVelocity.x,
            particle.velocity.y,
            smoothedHorizontalVelocity.z
        );
    }

    private bool TryResolveCanvasImpact(
        Vector3 previousPosition,
        Vector3 currentPosition,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out bool crossedCanvasPlane
    )
    {
        hitPoint = currentPosition;
        hitNormal = Vector3.up;
        crossedCanvasPlane = false;

        if (canvasPainter == null)
        {
            return false;
        }

        Transform surface = canvasPainter.transform;
        hitNormal = useExactCanvasPlaneCollision ? surface.up.normalized : Vector3.up;
        Vector3 planePoint = useExactCanvasPlaneCollision
            ? GetCanvasVisualPlanePoint(surface)
            : new Vector3(0f, GetCanvasVisualPlanePoint(surface).y, 0f);

        float previousDistance = Vector3.Dot(previousPosition - planePoint, hitNormal);
        float currentDistance = Vector3.Dot(currentPosition - planePoint, hitNormal);

        crossedCanvasPlane = previousDistance > 0f && currentDistance <= 0f;

        if (!crossedCanvasPlane)
        {
            return false;
        }

        float denominator = previousDistance - currentDistance;
        float t = denominator > 0.00001f ? previousDistance / denominator : 1f;
        t = Mathf.Clamp01(t);

        hitPoint = Vector3.Lerp(previousPosition, currentPosition, t);

        if (rejectImpactsOutsideCanvas && !IsPointInsideCanvasBounds(hitPoint))
        {
            return false;
        }

        return true;
    }


    private Vector3 GetCanvasVisualPlanePoint(Transform surface)
    {
        if (surface == null)
        {
            return Vector3.zero;
        }

        MeshFilter filter = surface.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null)
        {
            return surface.position;
        }

        Vector3 localTopCenter = new Vector3(0f, filter.sharedMesh.bounds.max.y, 0f);
        return surface.TransformPoint(localTopCenter);
    }
    private bool IsPointInsideCanvasBounds(Vector3 worldPoint)
    {
        if (canvasPainter == null)
        {
            return false;
        }

        Vector3 localPoint = canvasPainter.transform.InverseTransformPoint(worldPoint);
        float padding = Mathf.Max(0f, canvasBoundsPadding);

        return localPoint.x >= -0.5f - padding &&
               localPoint.x <= 0.5f + padding &&
               localPoint.z >= -0.5f - padding &&
               localPoint.z <= 0.5f + padding;
    }

    private void PaintImpact(Vector3 hitPoint, Vector3 surfaceNormal, PaintParticle particle)
    {
        float speed = particle.velocity.magnitude;

        float viscosityFactor = Mathf.Clamp(
            1.2f / Mathf.Max(particle.viscosity, 0.2f),
            0.4f,
            2f
        );

        float impactRadius = particle.radius;
        impactRadius *= impactRadiusMultiplier;
        impactRadius *= 1f + speed * 0.025f;
        impactRadius *= viscosityFactor;

        impactRadius = Mathf.Clamp(impactRadius, minImpactRadius, maxImpactRadius);

        if (impactEngine != null)
        {
            PaintImpactData impactData = new PaintImpactData
            {
                worldPosition = hitPoint,
                surfaceNormal = surfaceNormal,
                incomingVelocity = particle.velocity,
                paintColor = particle.color,
                particleRadius = impactRadius,
                particleMass = Mathf.Max(
                    0.0001f,
                    particle.radius * particle.radius * particle.radius * Mathf.Max(particle.viscosity, 0.2f) * 1000f
                ),
                viscosity = particle.viscosity,
                wetness = 1f,
                sourceId = 0
            };

            impactEngine.ProcessImpact(impactData);
        }
        else
        {
            canvasPainter.PaintImpactAtWorldPosition(
                hitPoint,
                particle.velocity,
                particle.color,
                impactRadius
            );
        }

        if (enableLegacySecondarySpray && impactEngine == null)
        {
            int sprayCount = Mathf.RoundToInt(
                Mathf.Clamp(speed * viscosityFactor, 0f, 8f)
            );

            Vector3 tangentA = Vector3.ProjectOnPlane(Vector3.right, surfaceNormal);
            if (tangentA.sqrMagnitude < 0.0001f)
            {
                tangentA = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(surfaceNormal, tangentA).normalized;

            for (int i = 0; i < sprayCount; i++)
            {
                Vector2 randomDirection = Random.insideUnitCircle.normalized;
                float randomDistance = Random.Range(impactRadius * 1.4f, impactRadius * 4f);

                Vector3 sprayPoint = hitPoint +
                    tangentA * randomDirection.x * randomDistance +
                    tangentB * randomDirection.y * randomDistance;

                float sprayRadius = impactRadius * Random.Range(0.12f, 0.35f);

                canvasPainter.PaintAtWorldPosition(
                    sprayPoint,
                    particle.color,
                    sprayRadius
                );
            }
        }
    }

    private void RemoveParticleAt(int index)
    {
        if (index < 0 || index >= particles.Count)
        {
            return;
        }

        PaintParticle particle = particles[index];

        ReleaseVisual(particle.visualIndex);

        particles.RemoveAt(index);
    }

    public void SetMaxParticlesSafely(int newMaxParticles)
    {
        maxParticles = Mathf.Clamp(newMaxParticles, 16, 1000000);
        CullToParticleBudget();
    }

    public void ApplyPerformanceBudget(int particleBudget, int visualBudget, int interactionChecks, bool interactionsEnabled, bool visualDropletsEnabled)
    {
        maxParticles = Mathf.Clamp(particleBudget, 16, 1000000);
        maxParticlesSimulatedPerFrame = Mathf.Clamp(particleBudget, 16, 1000000);
        maxInteractionChecks = Mathf.Clamp(interactionChecks, 0, 64);
        enableParticleInteraction = interactionsEnabled;
        showVisualDroplets = visualDropletsEnabled;
        visualPoolSize = Mathf.Clamp(visualBudget, 0, 10000);
        CullToParticleBudget();
    }

    private void CullToParticleBudget()
    {
        int safeBudget = Mathf.Clamp(Mathf.Min(maxParticles, maxParticlesSimulatedPerFrame), 0, 1000000);
        while (particles.Count > safeBudget)
        {
            RemoveParticleAt(0);
        }
    }

    public void ResetParticles()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            ReleaseVisual(particles[i].visualIndex);
        }

        particles.Clear();
    }

    public int ActiveParticleCount
    {
        get
        {
            return particles.Count;
        }
    }

    public int ActiveVisualDropletCount
    {
        get
        {
            if (visualPool == null)
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < visualPool.Length; i++)
            {
                if (visualPool[i] != null && visualPool[i].isActive)
                {
                    count++;
                }
            }

            return count;
        }
    }
}