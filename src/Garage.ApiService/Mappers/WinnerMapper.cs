using Riok.Mapperly.Abstractions;
using DataModels = Garage.ApiService.Data.Models;
using SharedModels = Garage.Shared.Models;

[Mapper]
public partial class WinnerMapper
{
    // The database model (DataModels.Winner) does not include the Image field,
    // so we ignore it in the mapping to SharedModels.Winner.
    [MapperIgnoreTarget(nameof(SharedModels.Winner.Image))]
    public partial SharedModels.Winner WinnerToWinnerDto(DataModels.Winner winner);
}
