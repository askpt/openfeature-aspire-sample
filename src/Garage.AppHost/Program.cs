using Aspire.Hosting.GitHub;
using Azure.Provisioning;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Resources;
using Azure.Provisioning.Roles;
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
var chatService = builder.AddUvicornApp("chatservice", "../Garage.ChatService/", "main:app")
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
    var defaultFlagsPath = Path.Combine(builder.AppHostDirectory, "flags", "flagd.json");
    var defaultFlagsBase64 = Convert.ToBase64String(File.ReadAllBytes(defaultFlagsPath));

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

        var seedScriptIdentity = new UserAssignedIdentity("seedScriptIdentity")
        {
            Name = BicepFunction.Interpolate($"seed-flags-mi-{BicepFunction.Take(BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id), 8)}"),
            Location = BicepFunction.GetResourceGroup().Location
        };
        infra.Add(seedScriptIdentity);

        var seedScriptBlobRoleAssignment = account.CreateRoleAssignment(StorageBuiltInRole.StorageBlobDataContributor, seedScriptIdentity);
        seedScriptBlobRoleAssignment.Name = BicepFunction.CreateGuid(account.Id, "seedflagsblobrole");
        infra.Add(seedScriptBlobRoleAssignment);

        // Seed flags/flagd.json during infrastructure provisioning so flagd can start on first deploy.
        var seedFlagsScript = new AzureCliScript("seedFlags")
        {
            Name = BicepFunction.Interpolate($"seed-flags-{BicepFunction.Take(BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id, BicepFunction.GetDeployment().Name), 8)}"),
            Location = BicepFunction.GetResourceGroup().Location,
            Identity = new ArmDeploymentScriptManagedIdentity
            {
                IdentityType = ArmDeploymentScriptManagedIdentityType.UserAssigned,
                UserAssignedIdentities =
                {
                    ["${seedScriptIdentity.id}"] = new UserAssignedIdentityDetails()
                }
            },
            AzCliVersion = "2.60.0",
            CleanupPreference = ScriptCleanupOptions.Always,
            ForceUpdateTag = BicepFunction.GetDeployment().Name,
            RetentionInterval = TimeSpan.FromDays(1),
            Timeout = TimeSpan.FromMinutes(10),
            ScriptContent = """
                set -euo pipefail

                az login --identity --username "$SEED_IDENTITY_CLIENT_ID" --allow-no-subscriptions >/dev/null
                BLOB_EXISTS=$(az storage blob exists --account-name "$AZURE_STORAGE_ACCOUNT" --container-name "$FLAGS_CONTAINER" --name "$FLAGS_BLOB_NAME" --auth-mode login --query exists -o tsv)

                if [ "$BLOB_EXISTS" != "true" ]; then
                                    echo "$FLAGS_FILE_BASE64" | base64 -d > /tmp/flagd.json
                  az storage blob upload --account-name "$AZURE_STORAGE_ACCOUNT" --container-name "$FLAGS_CONTAINER" --name "$FLAGS_BLOB_NAME" --file /tmp/flagd.json --overwrite false --content-type application/json --auth-mode login
                fi
                """
        };

        seedFlagsScript.EnvironmentVariables.Add(new ScriptEnvironmentVariable
        {
            Name = "AZURE_STORAGE_ACCOUNT",
            Value = account.Name
        });
        seedFlagsScript.EnvironmentVariables.Add(new ScriptEnvironmentVariable
        {
            Name = "SEED_IDENTITY_CLIENT_ID",
            Value = seedScriptIdentity.ClientId
        });
        seedFlagsScript.EnvironmentVariables.Add(new ScriptEnvironmentVariable
        {
            Name = "FLAGS_CONTAINER",
            Value = "flags"
        });
        seedFlagsScript.EnvironmentVariables.Add(new ScriptEnvironmentVariable
        {
            Name = "FLAGS_BLOB_NAME",
            Value = "flagd.json"
        });
        seedFlagsScript.EnvironmentVariables.Add(new ScriptEnvironmentVariable
        {
            Name = "FLAGS_FILE_BASE64",
            Value = defaultFlagsBase64
        });

        infra.Add(seedFlagsScript);

        infra.Add(new ProvisioningOutput("accountName", typeof(string))
        {
            Value = account.Name
        });
        infra.Add(new ProvisioningOutput("seedScriptId", typeof(string))
        {
            Value = seedFlagsScript.Id
        });
    });

    var accountName = flagsStorage.GetOutput("accountName");
    var seedScriptId = flagsStorage.GetOutput("seedScriptId");

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

    // flagd: depends on infra seed output so blob exists before sync starts.
    flagd = flagd
        .WithEnvironment("FLAGS_SEED_SCRIPT_ID", seedScriptId)
        .WithReference(flagsBlobs)
        .WithEnvironment("AZURE_STORAGE_ACCOUNT", accountName)
        .WithArgs("--uri", "azblob://flags/flagd.json");

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
