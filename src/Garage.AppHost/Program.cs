var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Container App Environment for publishing
var containerAppEnvironment = builder
    .AddAzureContainerAppEnvironment("cae");

// Add GitHub Models (requires GitHub PAT)
var githubToken = builder.AddParameter("github-token", secret: true);
var chatModel = builder.AddGitHubModel("chat-model", "openai/gpt-4o")
    .WithApiKey(githubToken);

var cache = builder.AddAzureManagedRedis("cache").RunAsContainer();

var postgres = builder.AddAzurePostgresFlexibleServer("postgres").RunAsContainer();
var database = postgres.AddDatabase("garage-db");

var migration = builder.AddProject<Projects.Garage_ApiDatabaseSeeder>("database-seeder")
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

// Add Python chat service using Uvicorn (FastAPI/ASGI)
var chatService = builder.AddUvicornApp("chat-service", "../Garage.ChatService/", "main:app")
    .WithPip()
    .WithExternalHttpEndpoints()
    .WithReference(chatModel)
    .WithOtlpExporter()
    .WithHttpHealthCheck("/health");

// Only add flagd service for local development (not during publishing/deployment)
// Use DevCycle if is in publish mode
if (!builder.ExecutionContext.IsPublishMode)
{
    // Get the absolute path to the flags directory for the Go app
    var flagsPath = Path.Combine(builder.AppHostDirectory, "flags", "flagd.json");

    var flagsApi = builder.AddGolangApp("flags-api", "../Garage.FeatureFlags/")
        .WithHttpEndpoint(env: "PORT")
        .WithExternalHttpEndpoints()
        .WithEnvironment("FLAGS_FILE_PATH", flagsPath)
        .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
        .PublishAsDockerFile();

    var flagsDir = Path.GetDirectoryName(flagsPath)!;
    var flagd = builder.AddFlagd("flagd")
        .WithBindFileSync(flagsDir);

    var ofrepEndpoint = flagd.GetEndpoint("ofrep");

    apiService = apiService
        .WaitFor(flagd)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);

    webFrontend = webFrontend
        .WaitFor(flagd)
        .WithReference(flagsApi)
        .WaitFor(flagsApi)
        .WithReference(chatService)
        .WaitFor(chatService)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);

    migration = migration
        .WaitFor(flagd)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);

    flagsApi = flagsApi
        .WaitFor(flagd)
        .WithEnvironment("OFREP_ENDPOINT", ofrepEndpoint);

    chatService = chatService
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
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
