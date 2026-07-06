using UnityEngine;

[DefaultExecutionOrder(-90)]
public class RuntimeAdaptiveQualityGovernor : MonoBehaviour
{
    private const string RuntimeObjectName = "RuntimeAdaptiveQualityGovernor_Runtime";

    [Header("Adaptive Guard")]
    public bool enableAdaptiveGuard = true;
    public bool autoCreateOnSceneLoad = true;
    public bool allowAutomaticDowngrade = false;
    public bool allowAutomaticRestore = false;

    [Header("FPS Thresholds")]
    [Range(15f, 120f)] public float downgradeBelowFps = 28f;
    [Range(30f, 180f)] public float restoreAboveFps = 82f;
    [Range(0.15f, 2.0f)] public float sampleInterval = 0.5f;
    [Range(2, 20)] public int samplesBeforeAction = 14;
    [Range(0f, 20f)] public float startupGraceSeconds = 12f;

    [Header("Profiles")]
    public SimulationPerformanceOptimizer.PerformanceMode preferredProfile = SimulationPerformanceOptimizer.PerformanceMode.FinalBalanced;
    public SimulationPerformanceOptimizer.PerformanceMode emergencyProfile = SimulationPerformanceOptimizer.PerformanceMode.LowResource;

    [Header("References")]
    public SimulationPerformanceOptimizer performanceOptimizer;
    public SphParticleBudgetController sphBudgetController;

    [Header("Runtime Readout")]
    [SerializeField] private float smoothedFps;
    [SerializeField] private int lowFpsSampleCount;
    [SerializeField] private int highFpsSampleCount;
    [SerializeField] private string lastAction = "watching";

    private float sampleTimer;
    private int frameCounter;
    private float guardStartTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<RuntimeAdaptiveQualityGovernor>() != null)
        {
            return;
        }

        GameObject obj = new GameObject(RuntimeObjectName);
        obj.AddComponent<RuntimeAdaptiveQualityGovernor>();
    }

    private void Awake()
    {
        AutoFindReferences();
        guardStartTime = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        if (!enableAdaptiveGuard)
        {
            return;
        }

        frameCounter++;
        sampleTimer += Time.unscaledDeltaTime;

        if (sampleTimer < Mathf.Max(0.15f, sampleInterval))
        {
            return;
        }

        smoothedFps = frameCounter / Mathf.Max(0.0001f, sampleTimer);
        frameCounter = 0;
        sampleTimer = 0f;

        if (Time.realtimeSinceStartup - guardStartTime < startupGraceSeconds)
        {
            lastAction = "warming up";
            return;
        }

        EvaluateBudget();
    }

    private void AutoFindReferences()
    {
        if (performanceOptimizer == null) performanceOptimizer = Object.FindFirstObjectByType<SimulationPerformanceOptimizer>();
        if (sphBudgetController == null) sphBudgetController = Object.FindFirstObjectByType<SphParticleBudgetController>();
    }

    private void EvaluateBudget()
    {
        AutoFindReferences();

        if (smoothedFps < downgradeBelowFps)
        {
            lowFpsSampleCount++;
            highFpsSampleCount = 0;
        }
        else if (smoothedFps > restoreAboveFps)
        {
            highFpsSampleCount++;
            lowFpsSampleCount = 0;
        }
        else
        {
            lowFpsSampleCount = 0;
            highFpsSampleCount = 0;
        }

        if (allowAutomaticDowngrade && lowFpsSampleCount >= samplesBeforeAction)
        {
            if (performanceOptimizer != null && performanceOptimizer.CurrentMode != emergencyProfile)
            {
                performanceOptimizer.ApplyProfile(emergencyProfile);
                lastAction = "auto-downgraded to " + emergencyProfile + " after FPS " + smoothedFps.ToString("0.0");
                Debug.LogWarning("[VR Adaptive Quality] " + lastAction);
            }

            lowFpsSampleCount = 0;
        }

        if (allowAutomaticRestore && highFpsSampleCount >= samplesBeforeAction)
        {
            if (performanceOptimizer != null && performanceOptimizer.CurrentMode != preferredProfile)
            {
                performanceOptimizer.ApplyProfile(preferredProfile);
                if (sphBudgetController != null && preferredProfile == SimulationPerformanceOptimizer.PerformanceMode.UltraSPHPreparation)
                {
                    sphBudgetController.ApplyHighScalePreparation(false);
                }

                lastAction = "auto-restored to " + preferredProfile + " after FPS " + smoothedFps.ToString("0.0");
                Debug.Log("[VR Adaptive Quality] " + lastAction);
            }

            highFpsSampleCount = 0;
        }
    }

    public float SmoothedFps => smoothedFps;
    public string LastAction => lastAction;
}
