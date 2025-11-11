using System.Text.Json;
using Garage.ApiService.Data;
using Garage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using OpenFeature;
using OpenFeature.Model;

namespace Garage.ApiService.Services;

public class WinnersService(
    GarageDbContext context,
    ILogger<WinnersService> logger,
    IFeatureClient featureClient)
    : IWinnersService
{
    public async Task<IEnumerable<Winner>> GetAllWinnersAsync()
    {
        var evaluationContext = EvaluationContext.Builder()
            .SetTargetingKey(Guid.NewGuid().ToString())
            .Build();

        var winners = await featureClient.GetBooleanValueAsync("enable-database-winners", false, evaluationContext)
            ? await GetAllDatabaseWinnersAsync()
            : await GetAllJsonWinnersAsync();

        var count = await featureClient.GetIntegerDetailsAsync("winners-count", 5, evaluationContext);

        return winners.Take(count.Value);
    }

    private async Task<IEnumerable<Winner>> GetAllDatabaseWinnersAsync()
    {
        try
        {
            var winnersDatabase = await context.Winners
                .OrderByDescending(w => w.Year)
                .ToListAsync();

            var mapper = new WinnerMapper();

            return winnersDatabase.Select(mapper.WinnerToWinnerDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve all Le Mans winners");
            return [];
        }
    }

    private async Task<IEnumerable<Winner>> GetAllJsonWinnersAsync()
    {
        await SlowDownAsync();
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "winners.json");
        try
        {
            var jsonData = await File.ReadAllTextAsync(dataFilePath);
            var winners = JsonSerializer.Deserialize<Winner[]>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return winners?.OrderByDescending(w => w.Year) ?? Enumerable.Empty<Winner>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read winners data from JSON file: {FilePath}", dataFilePath);
            return [];
        }
    }

    private async Task SlowDownAsync()
    {
        var evaluationContext = EvaluationContext.Builder()
        .SetTargetingKey(Guid.NewGuid().ToString())
        .Build();

        // Simulate a slow operation
        var delay = await featureClient.GetIntegerValueAsync("SlowOperationDelay", 0, evaluationContext);
        await Task.Delay(delay);
    }
}
