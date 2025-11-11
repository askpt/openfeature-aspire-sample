var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Container App Environment for publishing
var containerAppEnvironment = builder
    .AddAzureContainerAppEnvironment("cae");

var serverKey = builder.AddParameter("devcycle-server-key", secret: true);
var devcycleUrl = builder.Configuration["DevCycle:Url"] ?? null;

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("garage-db");

var migration = builder.AddProject<Projects.Garage_DatabaseSeeder>("database-seeder")
    .WithReference(database)
    .WithEnvironment("DEVCYCLE__URL", devcycleUrl)
    .WithEnvironment("DEVCYCLE__SERVERKEY", serverKey)
    .WaitFor(database);

// Only add flagd service for local development (not during publishing/deployment)
var isLocalDevelopment = !builder.ExecutionContext.IsPublishMode;
var flagd = isLocalDevelopment
    ? builder.AddFlagd("flagd")
        .WithBindFileSync("./flags")
    : null;

var ofrepEndpoint = flagd?.GetEndpoint("ofrep");

var apiService = builder.AddProject<Projects.Garage_ApiService>("apiservice")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(cache)
    .WithEnvironment("DEVCYCLE__URL", devcycleUrl)
    .WithEnvironment("DEVCYCLE__SERVERKEY", serverKey)
    .WaitFor(cache)
    .WaitFor(migration)
    .PublishAsAzureContainerApp((infra, app) =>
    {
    })
    .WithHttpHealthCheck("/health");

var webFrontend = builder.AddNpmApp("webfrontend", "../Garage.React/");

// Only reference flagd in development
if (isLocalDevelopment && flagd != null && ofrepEndpoint != null)
{
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

webFrontend
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithEnvironment("DEVCYCLE__URL", devcycleUrl)
    .WithEnvironment("DEVCYCLE__SERVERKEY", serverKey)
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
