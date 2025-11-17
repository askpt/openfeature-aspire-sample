var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Container App Environment for publishing
var containerAppEnvironment = builder
    .AddAzureContainerAppEnvironment("cae");

var cache = builder.AddAzureRedis("cache").RunAsContainer();

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
        .WithReference(ofrepEndpoint)
        .WaitFor(flagd);

    webFrontend = webFrontend
        .WithReference(ofrepEndpoint)
        .WaitFor(flagd);

    migration = migration
        .WithReference(ofrepEndpoint)
        .WaitFor(flagd);
}
else
{
    var serverKey = builder.AddParameter("devcycle-server-key", secret: true);
    var devcycleUrl = builder.Configuration["DevCycle:Url"] ?? null;

    webFrontend = webFrontend
        .WithEnvironment("DEVCYCLE__URL", devcycleUrl)
        .WithEnvironment("DEVCYCLE__SERVERKEY", serverKey);

    apiService = apiService
        .WithEnvironment("DEVCYCLE__URL", devcycleUrl)
        .WithEnvironment("DEVCYCLE__SERVERKEY", serverKey);

    migration = migration
        .WithEnvironment("DEVCYCLE__URL", devcycleUrl)
        .WithEnvironment("DEVCYCLE__SERVERKEY", serverKey);
}

webFrontend
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
