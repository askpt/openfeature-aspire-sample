namespace Garage.ServiceDefaults.Services;

public interface IFeatureFlags
{
    public int SlowOperationDelay { get; }
    public bool EnableDatabaseWinners { get; }
    bool EnableStatsHeader { get; }
    bool EnableTabs { get; }
}
