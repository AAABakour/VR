using System.Collections.Generic;
using UnityEngine;

public class SimulationLifecycleManager : MonoBehaviour
{
    [Header("Profile")]
    public SimulationProfile activeProfile;
    public bool applyProfileOnStart = true;

    [Header("Subsystem Discovery")]
    public bool discoverSubsystemsInChildren = true;
    public bool discoverSubsystemsInScene = false;
    public MonoBehaviour[] explicitSubsystemBehaviours;

    [Header("State")]
    [SerializeField]
    private bool isPaused;

    private readonly List<ISimulationSubsystem> subsystems = new List<ISimulationSubsystem>();

    public bool IsPaused
    {
        get { return isPaused; }
    }

    public int RegisteredSubsystemCount
    {
        get { return subsystems.Count; }
    }

    private void Awake()
    {
        RefreshSubsystemCache();
    }

    private void Start()
    {
        if (applyProfileOnStart && activeProfile != null)
        {
            ApplyProfile(activeProfile);
        }
    }

    [ContextMenu("Refresh Subsystem Cache")]
    public void RefreshSubsystemCache()
    {
        subsystems.Clear();

        if (explicitSubsystemBehaviours != null)
        {
            for (int i = 0; i < explicitSubsystemBehaviours.Length; i++)
            {
                AddSubsystemFromBehaviour(explicitSubsystemBehaviours[i]);
            }
        }

        if (discoverSubsystemsInChildren)
        {
            MonoBehaviour[] childBehaviours = GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < childBehaviours.Length; i++)
            {
                AddSubsystemFromBehaviour(childBehaviours[i]);
            }
        }

        if (discoverSubsystemsInScene)
        {
            MonoBehaviour[] sceneBehaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < sceneBehaviours.Length; i++)
            {
                AddSubsystemFromBehaviour(sceneBehaviours[i]);
            }
        }
    }

    [ContextMenu("Reset All Subsystems")]
    public void ResetAllSubsystems()
    {
        RemoveDestroyedSubsystems();

        for (int i = 0; i < subsystems.Count; i++)
        {
            subsystems[i].ResetSubsystem();
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        RemoveDestroyedSubsystems();

        for (int i = 0; i < subsystems.Count; i++)
        {
            subsystems[i].SetPaused(paused);
        }
    }

    public void TogglePaused()
    {
        SetPaused(!isPaused);
    }

    public void ApplyProfile(SimulationProfile profile)
    {
        activeProfile = profile;
        RemoveDestroyedSubsystems();

        if (profile == null)
        {
            return;
        }

        for (int i = 0; i < subsystems.Count; i++)
        {
            subsystems[i].ApplySimulationProfile(profile);
        }
    }

    private void AddSubsystemFromBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null || behaviour == this)
        {
            return;
        }

        ISimulationSubsystem subsystem = behaviour as ISimulationSubsystem;

        if (subsystem == null || subsystems.Contains(subsystem))
        {
            return;
        }

        subsystems.Add(subsystem);
    }

    private void RemoveDestroyedSubsystems()
    {
        for (int i = subsystems.Count - 1; i >= 0; i--)
        {
            if (!IsSubsystemAlive(subsystems[i]))
            {
                subsystems.RemoveAt(i);
            }
        }
    }

    private bool IsSubsystemAlive(ISimulationSubsystem subsystem)
    {
        if (subsystem == null)
        {
            return false;
        }

        if (subsystem is Object unityObject)
        {
            return unityObject != null;
        }

        return true;
    }
}
