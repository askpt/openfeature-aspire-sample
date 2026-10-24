using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Garage.ApiModel.Data;
using Garage.ApiService.Services;
using Garage.ApiService.Telemetry;
using Garage.ServiceDefaults;
using Garage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenFeature;
using OpenFeature.Model;

var builder = WebApplication.CreateBuilder(args);

// Add Redis distributed cache.
builder.AddRedisDistributedCache("cache");

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add database
builder.AddAzureNpgsqlDbContext<GarageDbContext>("garage-db", configureDbContextOptions: options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            if (await context.Set<Garage.ApiModel.Data.Models.Winner>().AnyAsync(cancellationToken))
            {
                return;
            }

            var jsonFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "winners.json");
            if (!File.Exists(jsonFilePath))
            {
                return;
            }

            var jsonData = await File.ReadAllTextAsync(jsonFilePath, cancellationToken);
            var winners = JsonSerializer.Deserialize<Garage.ApiModel.Data.Models.Winner[]>(
                jsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (winners is { Length: > 0 })
            {
                await context.Set<Garage.ApiModel.Data.Models.Winner>().AddRangeAsync(winners, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }
        });

        options.EnableSensitiveDataLogging();
    }
});

// Add services to the container.
builder.Services.AddProblemDetails();

// Register both the winner service
builder.Services.AddScoped<IWinnersService, WinnersService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(OpenApiHelpers.ConfigureOpenApi);

// Register Meter and source-generated metrics
builder.Services.TryAddSingleton(s =>
    s.GetRequiredService<IMeterFactory>().Create("Garage.ApiService", "1.0.0"));
builder.Services.TryAddSingleton(s =>
    ApiMetrics.CreateRequestCounter(s.GetRequiredService<Meter>()));
builder.Services.TryAddSingleton(s =>
    ApiMetrics.CreateRequestHistogram(s.GetRequiredService<Meter>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<GarageDbContext>();
    var strategy = context.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () => await context.Database.EnsureCreatedAsync());
}

// Le Mans Winners API endpoints
const string LeMansWinners = "GetAllLeMansWinners";
app.MapGet("/lemans/winners", async (IWinnersService winnersService, ILogger<Program> logger) =>
    {
        // Set correct trace status
        using var activity = Activity.Current;
        try
        {
            var winners = await winnersService.GetAllWinnersAsync();
            activity?.SetTag("feature.winners-count", winners.Count());
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Results.Ok(winners);
        }
        catch (Exception ex)
        {
            // Set trace status to error
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Log the exception (logging is configured in service defaults)
            logger.LogError(ex, "An error occurred while retrieving Le Mans winners.");

            return Results.Problem("An unexpected error occurred while retrieving winners.", statusCode: StatusCodes.Status500InternalServerError);
        }
    })
    .WithName(LeMansWinners)
    .WithSummary("List Le Mans 24 Hours winners")
    .WithDescription("Returns Le Mans winners with car and driver details. The data source and result size are controlled by feature flags including enable-database-winners and winners-count.")
    .Produces<IEnumerable<Winner>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .WithTags("Le Mans Winners");

// Test endpoint for the feature flags (error/slow responses)
app.MapGet("/test/error", async (IFeatureClient featureClient, RequestCounter? requestCounter, RequestHistogram? requestHistogram, ILogger<Program> logger) =>
{
    using var activity = Activity.Current;
    IResult result;
    var stopwatch = Stopwatch.StartNew();
    var isError = false;

    try
    {
        var delay = await featureClient.GetIntegerDetailsAsync("simulate-delay-ms", 100);
        await Task.Delay(delay.Value);

        if (await featureClient.GetBooleanValueAsync("simulate-error", false))
        {
            throw new InvalidOperationException("Simulated error for testing feature flags.");
        }

        result = Results.Ok(new { message = "Test endpoint executed successfully." });
    }
    catch (Exception ex)
    {
        // Set trace status to error
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

        // Log the exception (logging is configured in service defaults)
        logger.LogError(ex, "An error occurred during the test endpoint.");
        isError = true;
        result = Results.Problem("An unexpected error occurred during the test endpoint.", statusCode: StatusCodes.Status500InternalServerError);
    }

    stopwatch.Stop();

    requestCounter?.Add(1, "error", "test");
    requestHistogram?.Record(stopwatch.ElapsedMilliseconds, "error", "test", isError);
    activity?.SetStatus(isError ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
    return result;
});

// Test endpoint for the feature flags (tracking API): scores the `prompt-file` variant served to a
// random user and reports it through OpenFeature tracking, so the quality of the active prompt can
// be charted while k6 drives traffic and the flag is changed.
var promptScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
{
    ["unreliable"] = 0,
    ["brief"] = 3,
    ["casual"] = 5,
    ["expert"] = 10,
};

app.MapGet("/test/prompt-score", async (IFeatureClient featureClient, RequestCounter? requestCounter, RequestHistogram? requestHistogram) =>
{
    var stopwatch = Stopwatch.StartNew();

    var evaluationContext = EvaluationContext.Builder()
        .SetTargetingKey(Guid.NewGuid().ToString())
        .Build();

    var prompt = await featureClient.GetStringDetailsAsync("prompt-file", "expert", evaluationContext);
    var variant = prompt.Variant ?? prompt.Value;
    var score = promptScores.GetValueOrDefault(variant);

    featureClient.Track("prompt-score", evaluationContext, TrackingEventDetails.Builder()
        .SetValue(score)
        .Set("feature_flag.key", "prompt-file")
        .Set("feature_flag.result.variant", variant)
        .Build());

    stopwatch.Stop();
    requestCounter?.Add(1, "prompt-score", "test");
    requestHistogram?.Record(stopwatch.ElapsedMilliseconds, "prompt-score", "test", false);

    return Results.Ok(new { promptFile = prompt.Value, variant, score });
});

app.MapDefaultEndpoints();

app.Run();
