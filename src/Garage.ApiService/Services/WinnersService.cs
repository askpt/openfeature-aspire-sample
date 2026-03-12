using System.Text.Json;
using Garage.ApiModel.Data;
using Garage.ApiService.Mappers;
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

        var count = await featureClient.GetIntegerDetailsAsync("winners-count", 5, evaluationContext);

        return await featureClient.GetBooleanValueAsync("enable-database-winners", false, evaluationContext)
            ? await GetAllDatabaseWinnersAsync(count.Value)
            : await GetAllJsonWinnersAsync(count.Value);
    }

    private async Task<IEnumerable<Winner>> GetAllDatabaseWinnersAsync(int count)
    {
        try
        {
            var winnersDatabase = await context.Winners
                .AsNoTracking()
                .OrderByDescending(w => w.Year)
                .Take(count)
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

    private async Task<IEnumerable<Winner>> GetAllJsonWinnersAsync(int count)
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

            return winners?.OrderByDescending(w => w.Year).Take(count) ?? Enumerable.Empty<Winner>();
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
