# .NET bucket

`Directory.Build.props` sets `TreatWarningsAsErrors`, `Nullable=enable`, `LangVersion=latest`, `ImplicitUsings`. Any new warning fails the Release build in CI, so Step 4's build result is the primary verdict input; the rows below are what a green build does not prove.

| Check | Severity |
|---|---|
| A new `!` (null-forgiving) or `#pragma warning disable` / `<NoWarn>` hides a nullable or analyzer warning instead of fixing it. | Should |
| Package versions live in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); a `Version=` attribute on a `PackageReference` in a `.csproj` breaks restore. | Blocker |
| Async path: `.Result`, `.Wait()`, `Task.Run` around sync IO, `async void`, or a `CancellationToken` parameter that stops being forwarded. | Blocker |
| New minimal-API endpoint in `Garage.ApiService/Program.cs`: DTO in `Garage.Shared`, entity → DTO via the Mapperly `[Mapper]` in `Mappers/`, OpenAPI metadata via `Helpers/OpenApiHelpers.cs`, Redis-backed caching considered for read-heavy data, and reachable from the browser only if `vite.config.ts` `/api` proxy and `Garage.Web/default.conf.template` `location /api/` still route it. | Should |
| New unit of work carries a span from the class's `ActivitySource` (`WinnersService` pattern) and metrics through `Telemetry/ApiMetrics.cs`; `ILogger` calls use message templates (`"{Count}"`), not interpolation. | Should |
| EF Core model change in `Garage.ApiModel/Data/Models` ships with a migration under `Garage.ApiModel/Migrations` and a matching `Garage.ApiDatabaseSeeder` update. | Blocker |
| `Garage.ServiceDefaults/Extensions.cs` is shared by every .NET service: a change there is reviewed for all of them, including the OpenFeature provider setup in `Providers/`. | Should |

## AppHost (`src/Garage.AppHost/Program.cs`)

| Check | Severity |
|---|---|
| A new resource the app depends on is wired with `.WithReference(...)` and `.WaitFor(...)` on each consumer; the env var name the consumer reads matches the one set by `WithEnvironment` (Go: `FLAGS_FILE_PATH`, `OFREP_ENDPOINT`; Python: `CHAT_MODEL_URI`, `CHAT_MODEL_API_PATH`, `CHAT_MODEL_KEY`, `CHAT_MODEL_MODELNAME`). | Blocker |
| A removed `.WaitFor(...)` or an added `.WithExplicitStart()` changes startup order or what `aspire run` brings up; the commit or PR states why, and `README.md` still describes what a user sees after `aspire run`. | Should |
| Run vs publish: anything that only exists locally (Foundry Local, DevTunnel, file-backed flagd, k6, browser resources) stays under `!builder.ExecutionContext.IsPublishMode`; anything Azure-only stays under `IsPublishMode`. | Blocker |
| The web frontend keeps `OTEL_EXPORTER_OTLP_PROTOCOL` literally `"http"`: `WithAppForwarding` picks the collector endpoint by that name, and a browser cannot speak gRPC. | Blocker |
| The `Aspire.AppHost.Sdk/<version>` attribute in `Garage.AppHost.csproj` matches the `Aspire.Hosting.*` line in `Directory.Packages.props`. | Should |
| New integration package is the version `aspire add` / `list integrations` reports for this Sdk, and the new resource is documented in `.github/copilot-instructions.md`. | Nit |

Deep audit of `.csproj` / `.props` structure itself: hand off to `msbuild-code-review`; do not repeat it here.
