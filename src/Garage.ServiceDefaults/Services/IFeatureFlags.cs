namespace Garage.ServiceDefaults.Services;

public interface IFeatureFlags
{
    int SlowOperationDelay { get; }
    bool EnableDatabaseWinners { get; }
    bool EnableStatsHeader { get; }
    bool EnableTabs { get; }
}
