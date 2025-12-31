using Riok.Mapperly.Abstractions;
using DataModels = Garage.ApiModel.Data.Models;
using SharedModels = Garage.Shared.Models;

namespace Garage.ApiService.Mappers;

[Mapper]
public partial class WinnerMapper
{
    // The database model (DataModels.Winner) does not include the Image field,
    // so we ignore it in the mapping to SharedModels.Winner.
    [MapperIgnoreTarget(nameof(SharedModels.Winner.Image))]
    public partial SharedModels.Winner WinnerToWinnerDto(DataModels.Winner winner);
}
