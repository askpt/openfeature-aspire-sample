using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace Garage.ApiService.Telemetry;

[ExcludeFromCodeCoverage]
public static partial class ApiMetrics
{
    [Counter("action", "controller", Name = "app.request_counter")]
    public static partial RequestCounter CreateRequestCounter(Meter meter);

    [Histogram("action", "controller", "is_error", Name = "app.request_duration")]
    public static partial RequestHistogram CreateRequestHistogram(Meter meter);
}
