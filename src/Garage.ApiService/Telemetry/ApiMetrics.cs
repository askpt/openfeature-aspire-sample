using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace Garage.ApiService.Telemetry;

/// <summary>
/// Metrics for the API service using compile-time source generation.
/// </summary>
[ExcludeFromCodeCoverage]
public static partial class ApiMetrics
{
    /// <summary>
    /// Creates a request counter for API endpoint calls.
    /// </summary>
    /// <param name="meter">The meter instance.</param>
    /// <returns>A counter with action and controller dimensions.</returns>
    [Counter("action", "controller", Name = "app.request_counter")]
    public static partial RequestCounter CreateRequestCounter(Meter meter);
}
