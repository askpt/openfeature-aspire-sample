namespace Garage.Shared.Models;

public record Winner(
    int Year,
    string Manufacturer,
    string Model,
    string Engine,
    string Class,
    string[] Drivers,
    string? Image = null)
{
    public bool IsOwned { get; set; }
}
