using System.Text.Json;
using Garage.ApiService.Data;
using Garage.ApiService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Garage.DatabaseSeeder;

public class DatabaseSeederService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseSeederService> _logger;
    private readonly IHostEnvironment _environment;

    public DatabaseSeederService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseSeederService> logger,
        IHostEnvironment environment)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _environment = environment;
    }

    public async Task SeedDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GarageDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync(cancellationToken);

        // Check if data already exists
        if (await context.Winners.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Database already contains winner data, skipping seed");
            return;
        }

        // Read JSON data from the shared Data directory
        var jsonFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "winners.json");
        var jsonData = await File.ReadAllTextAsync(jsonFilePath, cancellationToken);
        var winners = JsonSerializer.Deserialize<Winner[]>(jsonData, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (winners == null || winners.Length == 0)
        {
            _logger.LogWarning("No winner data found in JSON file");
            return;
        }

        // Add winners to database
        await context.Winners.AddRangeAsync(winners, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully seeded database with {Count} Le Mans winners", winners.Length);
    }
}
