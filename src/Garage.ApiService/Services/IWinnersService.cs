using Garage.Shared.Models;

namespace Garage.ApiService.Services;

public interface IWinnersService
{
    Task<IEnumerable<Winner>> GetAllWinnersAsync();
}
