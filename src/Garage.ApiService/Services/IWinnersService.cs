using Garage.Shared.Models;

namespace Garage.ApiService.Services;

/// <summary>
/// Provides Le Mans winner data for API endpoints.
/// </summary>
public interface IWinnersService
{
    /// <summary>
    /// Retrieves winners from the configured data source.
    /// </summary>
    /// <returns>
    /// A collection of Le Mans winners ordered by year descending.
    /// </returns>
    Task<IEnumerable<Winner>> GetAllWinnersAsync();
}
