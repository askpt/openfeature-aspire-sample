using Garage.ApiDatabaseSeeder;
using Garage.ServiceDefaults;
using Garage.ApiModel.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.AddServiceDefaults();

// Add database
builder.AddAzureNpgsqlDbContext<GarageDbContext>("garage-db");

builder.Services.AddSingleton<DatabaseSeederService>();

var host = builder.Build();

host.Run();
