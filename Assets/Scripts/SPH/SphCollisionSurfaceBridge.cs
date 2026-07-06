using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(80)]
public class SphCollisionSurfaceBridge : MonoBehaviour
{
    private const string RuntimeObjectName = "SphCollisionSurfaceBridge_Runtime";

    [System.Serializable]
    public struct SphImpactSample
    {
        public Vector3 worldPosition;
        public Vector3 surfaceNormal;
        public Vector3 incomingVelocity;
        public Color color;
        public float radius;
        public float mass;
        public float viscosity;
        public float wetness;
    }

    [Header("References")]
    public PaintImpactEngineV2 impactEngine;
    public CanvasPainter fallbackCanvasPainter;

    [Header("Batching")]
    public bool highScaleMode = true;
    [Range(64, 65536)] public int maxImpactsPerFrame = 8192;
    [Range(1, 32)] public int coalesceNearbyHitsEveryN = 4;
    [Range(0.0001f, 0.05f)] public float minimumImpactRadius = 0.006f;
    [Range(0.01f, 0.18f)] public float maximumImpactRadius = 0.075f;

    [Header("Phase 05 Cohesive Impact Coalescing")]
    public bool enableSpatialCoalescing = true;
    [Range(0.01f, 0.18f)] public float clusterCellSize = 0.045f;
    [Range(8, 4096)] public int maxClusteredImpactsPerFrame = 512;
    [Range(0.15f, 2.5f)] public float clusteredRadiusGain = 0.58f;
    public bool velocityWeightedClusters = true;

    [Header("Runtime Stats")]
    [SerializeField] private int acceptedImpactsLastFrame;
    [SerializeField] private int droppedImpactsLastFrame;
    [SerializeField] private int clusteredImpactsLastFrame;
    [SerializeField] private int totalForwardedImpacts;

    private struct ImpactCluster
    {
        public Vector3 positionSum;
        public Vector3 normalSum;
        public Vector3 velocitySum;
        public Color colorSum;
        public float radiusMax;
        public float massSum;
        public float viscositySum;
        public float wetnessSum;
        public int count;
    }

    private readonly Dictionary<int, ImpactCluster> clusters = new Dictionary<int, ImpactCluster>(512);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<SphCollisionSurfaceBridge>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<SphCollisionSurfaceBridge>();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (impactEngine == null) impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        if (fallbackCanvasPainter == null) fallbackCanvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
    }

    public int SubmitImpactBatch(SphImpactSample[] samples, int count)
    {
        if (samples == null || count <= 0)
        {
            acceptedImpactsLastFrame = 0;
            droppedImpactsLastFrame = 0;
            clusteredImpactsLastFrame = 0;
            return 0;
        }

        AutoFindReferences();

        int safeCount = Mathf.Min(count, samples.Length);
        int limit = highScaleMode ? Mathf.Min(safeCount, maxImpactsPerFrame) : safeCount;

        int forwarded;
        if (highScaleMode && enableSpatialCoalescing)
        {
            forwarded = ForwardClusteredImpacts(samples, limit);
        }
        else
        {
            int stride = highScaleMode ? Mathf.Max(1, coalesceNearbyHitsEveryN) : 1;
            forwarded = 0;
            for (int i = 0; i < limit; i += stride)
            {
                ForwardImpact(samples[i]);
                forwarded++;
            }
            clusteredImpactsLastFrame = 0;
        }

        acceptedImpactsLastFrame = forwarded;
        droppedImpactsLastFrame = Mathf.Max(0, safeCount - limit);
        totalForwardedImpacts += forwarded;
        return forwarded;
    }

    public void SubmitSingleImpact(Vector3 worldPosition, Vector3 normal, Vector3 incomingVelocity, Color color, float radius, float mass, float viscosity)
    {
        SubmitSingleImpact(worldPosition, normal, incomingVelocity, color, radius, mass, viscosity, 1f);
    }

    public void SubmitSingleImpact(Vector3 worldPosition, Vector3 normal, Vector3 incomingVelocity, Color color, float radius, float mass, float viscosity, float wetness)
    {
        SphImpactSample sample = new SphImpactSample
        {
            worldPosition = worldPosition,
            surfaceNormal = normal,
            incomingVelocity = incomingVelocity,
            color = color,
            radius = radius,
            mass = mass,
            viscosity = viscosity,
            wetness = wetness
        };

        ForwardImpact(sample);
        acceptedImpactsLastFrame++;
        droppedImpactsLastFrame = 0;
        totalForwardedImpacts++;
    }


    private int ForwardClusteredImpacts(SphImpactSample[] samples, int limit)
    {
        clusters.Clear();
        float cellSize = Mathf.Max(0.005f, clusterCellSize);
        int safeClusterLimit = Mathf.Max(1, maxClusteredImpactsPerFrame);

        for (int i = 0; i < limit; i++)
        {
            SphImpactSample sample = samples[i];
            int key = BuildClusterKey(sample.worldPosition, cellSize);

            if (!clusters.TryGetValue(key, out ImpactCluster cluster))
            {
                if (clusters.Count >= safeClusterLimit)
                {
                    continue;
                }

                cluster = new ImpactCluster();
            }

            float mass = Mathf.Max(0.00001f, sample.mass);
            float velocityWeight = velocityWeightedClusters ? Mathf.Lerp(1f, 2.2f, Mathf.Clamp01(sample.incomingVelocity.magnitude / 8f)) : 1f;
            float weight = mass * velocityWeight;

            cluster.positionSum += sample.worldPosition * weight;
            cluster.normalSum += (sample.surfaceNormal.sqrMagnitude > 0.0001f ? sample.surfaceNormal.normalized : Vector3.up) * weight;
            cluster.velocitySum += sample.incomingVelocity * weight;
            cluster.colorSum += sample.color * weight;
            cluster.radiusMax = Mathf.Max(cluster.radiusMax, sample.radius);
            cluster.massSum += weight;
            cluster.viscositySum += Mathf.Max(0.15f, sample.viscosity) * weight;
            cluster.wetnessSum += Mathf.Clamp01(sample.wetness <= 0f ? 1f : sample.wetness) * weight;
            cluster.count++;
            clusters[key] = cluster;
        }

        int forwarded = 0;
        foreach (KeyValuePair<int, ImpactCluster> item in clusters)
        {
            ImpactCluster cluster = item.Value;
            if (cluster.count <= 0 || cluster.massSum <= 0f)
            {
                continue;
            }

            float invMass = 1f / cluster.massSum;
            SphImpactSample merged = new SphImpactSample
            {
                worldPosition = cluster.positionSum * invMass,
                surfaceNormal = (cluster.normalSum * invMass).normalized,
                incomingVelocity = cluster.velocitySum * invMass,
                color = cluster.colorSum * invMass,
                radius = Mathf.Clamp(cluster.radiusMax * (1f + Mathf.Sqrt(cluster.count) * clusteredRadiusGain), minimumImpactRadius, maximumImpactRadius),
                mass = cluster.massSum,
                viscosity = cluster.viscositySum * invMass,
                wetness = cluster.wetnessSum * invMass
            };

            if (merged.surfaceNormal.sqrMagnitude < 0.0001f)
            {
                merged.surfaceNormal = Vector3.up;
            }

            ForwardImpact(merged);
            forwarded++;
        }

        clusteredImpactsLastFrame = Mathf.Max(0, limit - forwarded);
        return forwarded;
    }

    private int BuildClusterKey(Vector3 position, float cellSize)
    {
        int x = Mathf.FloorToInt(position.x / cellSize);
        int y = Mathf.FloorToInt(position.y / cellSize);
        int z = Mathf.FloorToInt(position.z / cellSize);
        unchecked
        {
            int key = 17;
            key = key * 31 + x * 73856093;
            key = key * 31 + y * 19349663;
            key = key * 31 + z * 83492791;
            return key;
        }
    }

    private void ForwardImpact(SphImpactSample sample)
    {
        float radius = Mathf.Clamp(sample.radius, minimumImpactRadius, maximumImpactRadius);
        float viscosity = Mathf.Max(0.15f, sample.viscosity);
        float mass = Mathf.Max(0.00001f, sample.mass);
        Vector3 safeNormal = sample.surfaceNormal.sqrMagnitude > 0.0001f ? sample.surfaceNormal.normalized : Vector3.up;

        if (impactEngine != null)
        {
            PaintImpactData data = new PaintImpactData
            {
                worldPosition = sample.worldPosition,
                surfaceNormal = safeNormal,
                incomingVelocity = sample.incomingVelocity,
                paintColor = sample.color,
                particleRadius = radius,
                particleMass = mass,
                viscosity = viscosity,
                wetness = Mathf.Clamp01(sample.wetness <= 0f ? 1f : sample.wetness),
                sourceId = 9001
            };

            impactEngine.ProcessImpact(data);
        }
        else if (fallbackCanvasPainter != null)
        {
            fallbackCanvasPainter.PaintImpactAtWorldPosition(sample.worldPosition, sample.incomingVelocity, sample.color, radius);
        }
    }

    public void ResetBridgeStats()
    {
        acceptedImpactsLastFrame = 0;
        droppedImpactsLastFrame = 0;
        clusteredImpactsLastFrame = 0;
        totalForwardedImpacts = 0;
    }

    public int AcceptedImpactsLastFrame => acceptedImpactsLastFrame;
    public int DroppedImpactsLastFrame => droppedImpactsLastFrame;
    public int ClusteredImpactsLastFrame => clusteredImpactsLastFrame;
    public int TotalForwardedImpacts => totalForwardedImpacts;
    public string Phase05Line => "Phase05 collision bridge | accepted " + acceptedImpactsLastFrame + " | clustered " + clusteredImpactsLastFrame + " | dropped " + droppedImpactsLastFrame;
}
