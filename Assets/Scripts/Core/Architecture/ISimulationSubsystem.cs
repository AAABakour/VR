public interface ISimulationSubsystem
{
    string SubsystemName { get; }

    void ResetSubsystem();

    void SetPaused(bool paused);

    void ApplySimulationProfile(SimulationProfile profile);
}
