using System.Diagnostics;

namespace Garage.ApiDatabaseSeeder;

public class Worker : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    private readonly ILogger<Worker> _logger;
    private readonly DatabaseSeederService _databaseSeederService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public Worker(ILogger<Worker> logger, DatabaseSeederService databaseSeederService, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _databaseSeederService = databaseSeederService;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = s_activitySource.StartActivity("Migrating database", ActivityKind.Client);
        try
        {
            await _databaseSeederService.SeedDatabaseAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            activity?.AddException(ex);
            throw;
        }

        _hostApplicationLifetime.StopApplication();
    }
}
