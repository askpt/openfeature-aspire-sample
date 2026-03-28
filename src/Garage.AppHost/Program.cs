using Aspire.Hosting.GitHub;
using Azure.Provisioning;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Container App Environment for publishing
var containerAppEnvironment = builder
    .AddAzureContainerAppEnvironment("cae");

// Add GitHub Models (requires GitHub PAT)
var githubToken = builder.AddParameter("github-token", secret: true);
var chatModel = builder.AddGitHubModel("chat-model", GitHubModel.OpenAI.OpenAIGpt4o)
    .WithApiKey(githubToken)
    .WithHealthCheck();

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
    .WithHttpHealthCheck("/health")
    .PublishAsDockerFile();

if (builder.ExecutionContext.IsPublishMode)
{
    chatService = chatService
        .WithArgs("main:app", "--host", "0.0.0.0", "--port", "8000");
}

// Feature flags: flagd + flags-api deployed in both local and Azure modes
var flagd = builder.AddFlagd("flagd");

if (!builder.ExecutionContext.IsPublishMode)
{
    // Local development: use bind mount from host filesystem
    var flagsPath = Path.Combine(builder.AppHostDirectory, "flags", "flagd.json");
    var flagsDir = Path.GetDirectoryName(flagsPath)!;
    flagd.WithBindFileSync(flagsDir);

    var flagsApi = builder.AddGolangApp("flags-api", "../Garage.FeatureFlags/")
        .WithHttpEndpoint(env: "PORT")
        .WithExternalHttpEndpoints()
        .WithEnvironment("FLAGS_FILE_PATH", flagsPath)
        .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
        .PublishAsDockerFile();

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
    // Azure deployment: flagd reads from Azure Blob Storage (azblob:// sync),
    // flags-api reads/writes via gocloud.dev blob SDK — no shared volume needed
    var flagsStorage = builder.AddAzureStorage("flags-storage");
    var flagsBlobs = flagsStorage.AddBlobs("flags-blobs");

    // Provision a "flags" blob container and output the storage account name
    flagsStorage.ConfigureInfrastructure(infra =>
    {
        var account = infra.GetProvisionableResources()
            .OfType<StorageAccount>()
            .Single();

        var blobService = infra.GetProvisionableResources()
            .OfType<BlobService>()
            .SingleOrDefault();

        if (blobService is null)
        {
            blobService = new BlobService("default") { Parent = account };
            infra.Add(blobService);
        }

        var flagsContainer = new BlobContainer("flags") { Parent = blobService, Name = "flags" };
        infra.Add(flagsContainer);

        infra.Add(new ProvisioningOutput("accountName", typeof(string))
        {
            Value = account.Name
        });
    });

    var accountName = flagsStorage.GetOutput("accountName");

    // flagd: Azure Blob sync (polls blob for updates)
    flagd
        .WithReference(flagsBlobs)
        .WithEnvironment("AZURE_STORAGE_ACCOUNT", accountName)
        .WithArgs("--uri", "azblob://flags/flagd.json");

    // flags-api: reads/writes flagd.json in Azure Blob via gocloud.dev
    var flagsApi = builder.AddGolangApp("flags-api", "../Garage.FeatureFlags/")
        .WithHttpEndpoint(env: "PORT")
        .WithExternalHttpEndpoints()
        .WithReference(flagsBlobs)
        .WithEnvironment("AZURE_STORAGE_ACCOUNT", accountName)
        .WithEnvironment("FLAGS_BLOB_CONTAINER", "flags")
        .WithEnvironment("FLAGS_BLOB_NAME", "flagd.json")
        .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
        .PublishAsDockerFile();

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

webFrontend
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
