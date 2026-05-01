using System.Diagnostics;
using System.Text.Json;
using Garage.ApiModel.Data;
using Garage.ApiService.Mappers;
using Garage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using OpenFeature;
using OpenFeature.Model;

namespace Garage.ApiService.Services;

internal class WinnersService(
    GarageDbContext context,
    ILogger<WinnersService> logger,
    IFeatureClient featureClient)
    : IWinnersService
{
    private static readonly ActivitySource _activitySource = new("Garage.ApiService");
    private static readonly WinnerMapper _mapper = new();
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Retrieves winners using feature flags to select the data source and item count.
    /// </summary>
    public async Task<IEnumerable<Winner>> GetAllWinnersAsync()
    {
        // Create new activity for tracing feature flag evaluation and data retrieval
        using var activity = _activitySource.StartActivity(nameof(GetAllWinnersAsync));

        var evaluationContext = EvaluationContext.Builder()
            .SetTargetingKey(Guid.NewGuid().ToString())
            .Build();

        var count = await featureClient.GetIntegerDetailsAsync("winners-count", 5, evaluationContext);

        var list = await featureClient.GetBooleanValueAsync("enable-database-winners", false, evaluationContext)
            ? await GetAllDatabaseWinnersAsync(count.Value)
            : await GetAllJsonWinnersAsync(count.Value, evaluationContext);

        activity?.SetStatus(ActivityStatusCode.Ok);
        return list;
    }

    /// <summary>
    /// Retrieves winners from PostgreSQL through Entity Framework.
    /// </summary>
    private async Task<IEnumerable<Winner>> GetAllDatabaseWinnersAsync(int count)
    {
        // Create new activity for tracing feature flag evaluation and data retrieval
        using var activity = _activitySource.StartActivity(nameof(GetAllDatabaseWinnersAsync));

        try
        {
            var winnersDatabase = await context.Winners
                .AsNoTracking()
                .OrderByDescending(w => w.Year)
                .Take(count)
                .ToListAsync();

            var list = winnersDatabase.Select(_mapper.WinnerToWinnerDto);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return list;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve all Le Mans winners");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw; // Let the exception bubble up to be handled by the caller
        }
    }

    /// <summary>
    /// Retrieves winners from the JSON seed file.
    /// </summary>
    private async Task<IEnumerable<Winner>> GetAllJsonWinnersAsync(int count, EvaluationContext evaluationContext)
    {
        // Create new activity for tracing feature flag evaluation and data retrieval
        using var activity = _activitySource.StartActivity(nameof(GetAllJsonWinnersAsync));

        await SlowDownAsync(evaluationContext);
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "winners.json");
        try
        {
            var jsonData = await File.ReadAllTextAsync(dataFilePath);
            var winners = JsonSerializer.Deserialize<Winner[]>(jsonData, _jsonOptions);

            var list = winners?.OrderByDescending(w => w.Year).Take(count) ?? [];
            activity?.SetStatus(ActivityStatusCode.Ok);
            return list;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read winners data from JSON file: {FilePath}", dataFilePath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw; // Let the exception bubble up to be handled by the caller
        }
    }

    /// <summary>
    /// Applies an optional delay for demo and testing scenarios.
    /// </summary>
    private async Task SlowDownAsync(EvaluationContext evaluationContext)
    {
        // Simulate a slow operation
        var delay = await featureClient.GetIntegerValueAsync("slow-operation-delay", 0, evaluationContext);
        await Task.Delay(delay);
    }
}
