using Garage.ApiModel.Data;
using Garage.ApiService.Services;
using Garage.ServiceDefaults;

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
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Le Mans Winners API endpoints
app.MapGet("/lemans/winners", async (IWinnersService winnersService) =>
    {
        var winners = await winnersService.GetAllWinnersAsync();
        return Results.Ok(winners);
    })
    .WithName("GetAllLeMansWinners")
    .WithTags("Le Mans");

app.MapDefaultEndpoints();

app.Run();
