var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Container App Environment for publishing
var containerAppEnvironment = builder
    .AddAzureContainerAppEnvironment("cae");

var cache = builder.AddAzureRedis("cache").RunAsContainer();

var flagsApi = builder.AddGolangApp("flags-api", "../Garage.FeatureFlags/")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

var postgres = builder.AddAzurePostgresFlexibleServer("postgres").RunAsContainer();
var database = postgres.AddDatabase("garage-db");

var migration = builder.AddProject<Projects.Garage_DatabaseSeeder>("database-seeder")
    .WithReference(database)
    .WaitFor(database);

var apiService = builder.AddProject<Projects.Garage_ApiService>("apiservice")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(cache)
    .WaitFor(cache)
    .WaitFor(migration)
    .PublishAsAzureContainerApp((infra, app) =>
    {
    })
    .WithHttpHealthCheck("/health");

var webFrontend = builder.AddJavaScriptApp("web", "../Garage.Web/");

// Only add flagd service for local development (not during publishing/deployment)
// Use DevCycle if is in publish mode
if (!builder.ExecutionContext.IsPublishMode)
{
    var flagd = builder.AddFlagd("flagd")
        .WithBindFileSync("./flags");

    var ofrepEndpoint = flagd.GetEndpoint("ofrep");

    apiService = apiService
        .WaitFor(flagd)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);

    webFrontend = webFrontend
        .WaitFor(flagd)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);

    migration = migration
        .WaitFor(flagd)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);
}
else
{
    var serverKey = builder.AddParameter("devcycle-server-key", secret: true);
    var devcycleUrl = builder.Configuration["DevCycle:Url"] ?? null;

    // For web (nginx), set separate OFREP_AUTHORIZATION for simplicity
    webFrontend = webFrontend
        .WithEnvironment("OFREP_ENDPOINT", devcycleUrl)
        .WithEnvironment("OFREP_AUTHORIZATION", serverKey);

    // For .NET services, use OFREP_HEADERS with Authorization=<value> format
    apiService = apiService
        .WithEnvironment("OFREP_ENDPOINT", devcycleUrl)
        .WithEnvironment("OFREP_HEADERS", $"Authorization={serverKey}");

    migration = migration
        .WithEnvironment("OFREP_ENDPOINT", devcycleUrl)
        .WithEnvironment("OFREP_HEADERS", $"Authorization={serverKey}");
}

webFrontend
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(flagsApi)
    .WaitFor(flagsApi)
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
