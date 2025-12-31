using Garage.ApiDatabaseSeeder;
using Garage.ApiModel.Data;
using Garage.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<ApiDbInitializer>();

builder.AddServiceDefaults();

builder.Services.AddDbContextPool<GarageDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("garage-db"), sqlOptions =>
        sqlOptions.MigrationsAssembly("Garage.ApiDatabaseSeeder")
    ));
builder.EnrichAzureNpgsqlDbContext<GarageDbContext>();

var app = builder.Build();

app.Run();
