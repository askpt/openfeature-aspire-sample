using System.Diagnostics;
using Garage.ApiModel.Data;
using Garage.ApiService.Services;
using Garage.ServiceDefaults;
using Garage.Shared.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Redis distributed cache.
builder.AddRedisDistributedCache("cache");

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add database
builder.AddAzureNpgsqlDbContext<GarageDbContext>("garage-db");

// Add services to the container.
builder.Services.AddProblemDetails();

// Register both the winner service
builder.Services.AddScoped<IWinnersService, WinnersService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(OpenApiHelpers.ConfigureOpenApi);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Le Mans Winners API endpoints
const string LeMansWinners = "GetAllLeMansWinners";
app.MapGet("/lemans/winners", async (IWinnersService winnersService, ILogger<Program> logger) =>
    {
        // Set correct trace status
        using var activity = Activity.Current;
        try
        {
            activity?.SetTag("feature.winners-count", await winnersService.GetAllWinnersAsync().ContinueWith(t => t.Result.Count()));
            activity?.SetTag("feature.enable-database-winners", await winnersService.GetAllWinnersAsync().ContinueWith(t => t.Result.Any(w => w.IsOwned)));

            var winners = await winnersService.GetAllWinnersAsync();
            return Results.Ok(winners);
        }
        catch (Exception ex)
        {
            // Set trace status to error
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);

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

app.MapDefaultEndpoints();

app.Run();
