namespace Garage.ServiceDefaults.Services;

public class FeatureFlags : IFeatureFlags
{
    public int SlowOperationDelay => int.TryParse(Environment.GetEnvironmentVariable(nameof(SlowOperationDelay)), out var delay) ? delay : 1000;
    public bool EnableDatabaseWinners => bool.TryParse(Environment.GetEnvironmentVariable(nameof(EnableDatabaseWinners)), out var enable) ? enable : false;
    public bool EnableStatsHeader => bool.TryParse(Environment.GetEnvironmentVariable(nameof(EnableStatsHeader)), out var enable) ? enable : true; // Default to true
    public bool EnableTabs => bool.TryParse(Environment.GetEnvironmentVariable(nameof(EnableTabs)), out var enable) ? enable : true; // Default to true
}
