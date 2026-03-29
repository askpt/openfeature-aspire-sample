using Garage.ApiModel.Data;
using Garage.ApiService.Services;
using Garage.ServiceDefaults;
using Garage.Shared.Models;
using Microsoft.OpenApi;
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
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Description = "REST API for Le Mans winners data with feature-flag-driven behavior and caching.";
        document.Info.Title = "Garage API Service";
        document.Info.Version = "1.0.0";
        document.Tags = new HashSet<OpenApiTag>
        {
            new()
            {
                Name = "Le Mans Winners",
                Description = "Operations for retrieving Le Mans winners and related vehicle details."
            }
        };

        if (document.Components?.Schemas is { } schemas)
        {
            if (schemas.TryGetValue("ProblemDetails", out var problemDetailsSchema) &&
                problemDetailsSchema.Properties is { } problemProperties)
            {
                SetSchemaPropertyDescription(problemProperties, "type", "A URI reference that identifies the problem type.");
                SetSchemaPropertyDescription(problemProperties, "title", "A short, human-readable summary of the problem.");
                SetSchemaPropertyDescription(problemProperties, "status", "The HTTP status code generated for this problem.");
                SetSchemaPropertyDescription(problemProperties, "detail", "A human-readable explanation specific to this problem instance.");
                SetSchemaPropertyDescription(problemProperties, "instance", "A URI reference that identifies this specific problem occurrence.");
            }
        }

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, _, _) =>
    {
        if (operation.OperationId == "GetAllLeMansWinners" && operation.Responses is { } responses)
        {
            if (responses.TryGetValue("200", out var okResponse))
            {
                okResponse.Description = "Le Mans winners were successfully retrieved.";
            }

            if (responses.TryGetValue("500", out var errorResponse))
            {
                errorResponse.Description = "An unexpected server error occurred while retrieving winners.";
            }
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Le Mans Winners API endpoints
app.MapGet("/lemans/winners", async (IWinnersService winnersService) =>
    {
        var winners = await winnersService.GetAllWinnersAsync();
        return Results.Ok(winners);
    })
    .WithName("GetAllLeMansWinners")
    .WithSummary("List Le Mans 24 Hours winners")
    .WithDescription("Returns Le Mans winners with car and driver details. The data source and result size are controlled by feature flags including enable-database-winners and winners-count.")
    .Produces<IEnumerable<Winner>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .WithTags("Le Mans Winners");

app.MapDefaultEndpoints();

app.Run();

static void SetSchemaPropertyDescription(IDictionary<string, IOpenApiSchema> properties, string propertyName, string description)
{
    if (properties.TryGetValue(propertyName, out var schema))
    {
        schema.Description = description;
    }
}
