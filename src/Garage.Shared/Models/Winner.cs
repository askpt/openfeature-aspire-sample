using System.ComponentModel;

namespace Garage.Shared.Models;

/// <summary>
/// Represents a Le Mans 24 Hours winning car and its drivers.
/// </summary>
public record Winner(
    [property: Description("The year the race was won.")]
    int Year,
    [property: Description("The manufacturer of the winning car.")]
    string Manufacturer,
    [property: Description("The model name of the winning car.")]
    string Model,
    [property: Description("The engine specification of the winning car.")]
    string Engine,
    [property: Description("The race class of the winning car.")]
    string Class,
    [property: Description("The drivers who won the race with this car.")]
    string[] Drivers,
    [property: Description("Optional image URL for the winning car.")]
    string? Image = null)
{
    [Description("Whether this winning car is currently owned by the garage.")]
    public bool IsOwned { get; set; }
}
