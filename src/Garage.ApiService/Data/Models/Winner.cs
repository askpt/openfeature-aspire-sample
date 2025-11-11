namespace Garage.ApiService.Data.Models;

public record Winner(
    int Year,
    string Manufacturer,
    string Model,
    string Engine,
    string Class,
    string[] Drivers)
{
    public bool IsOwned { get; set; }
}
