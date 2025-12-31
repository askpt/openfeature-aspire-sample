using System.Diagnostics;
using Garage.ApiModel.Data;
using Microsoft.EntityFrameworkCore;

namespace Garage.ApiDatabaseSeeder;

public class ApiDbInitializer(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<ApiDbInitializer> logger) : BackgroundService
{
    private readonly ActivitySource _activitySource = new(hostEnvironment.ApplicationName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity(hostEnvironment.ApplicationName, ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GarageDbContext>();

            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedDatabaseAsync(dbContext, cancellationToken);

            logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "Database initialization failed");
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(GarageDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private async Task SeedDatabaseAsync(GarageDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Check if data already exists
            if (await dbContext.Winners.AnyAsync(cancellationToken))
            {
                logger.LogInformation("Database already contains winner data, skipping seed");
                return;
            }

            // Read JSON data from the shared Data directory
            var baseDir = AppContext.BaseDirectory;
            var jsonFilePath = Path.Combine(baseDir, "Data", "winners.json");

            // Log the path we're looking for to help diagnose issues
            logger.LogInformation("Looking for winners.json at: {Path}", jsonFilePath);

            if (!File.Exists(jsonFilePath))
            {
                logger.LogWarning("Winners JSON file not found at {Path}, skipping seed", jsonFilePath);
                return;
            }

            var jsonData = await File.ReadAllTextAsync(jsonFilePath, cancellationToken);
            var winners = System.Text.Json.JsonSerializer.Deserialize<Garage.ApiModel.Data.Models.Winner[]>(jsonData, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (winners == null || winners.Length == 0)
            {
                logger.LogWarning("No winner data found in JSON file");
                return;
            }

            // Add winners to database
            await dbContext.Winners.AddRangeAsync(winners, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Successfully seeded database with {Count} Le Mans winners", winners.Length);
        });
    }
}
