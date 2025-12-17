# GitHub Copilot Instructions for OpenFeature Aspire Sample

## Project Overview

This is an OpenFeature .NET OFREP Demo application - a Le Mans Winners Management System that demonstrates feature flag capabilities using OpenFeature and the Remote Evaluation Protocol (OFREP) in a .NET environment with a React frontend.

### Technology Stack

- **.NET**: 10.0 (latest version)
- **Go**: 1.25 (for Feature Flags API)
- **Frontend**: React 19.2 with TypeScript, Vite 7.2
- **Backend**: ASP.NET Core 10.0 with Entity Framework Core
- **Orchestration**: .NET Aspire 13.0
- **Feature Flags**: OpenFeature with OFREP and flagd provider
- **Database**: PostgreSQL with Entity Framework Core
- **Caching**: Redis via StackExchange.Redis
- **Telemetry**: OpenTelemetry integration
- **Mapping**: Riok.Mapperly for object mapping

### Project Structure

```
/
├── src/
│   ├── Garage.ApiService/      # REST API service
│   ├── Garage.AppHost/         # .NET Aspire orchestration
│   ├── Garage.DatabaseSeeder/  # Database initialization
│   ├── Garage.FeatureFlags/    # Go API for feature flag management
│   ├── Garage.ServiceDefaults/ # Shared services & feature flags
│   ├── Garage.Shared/          # Common models and DTOs
│   └── Garage.Web/             # React + Vite frontend
├── .github/                    # CI/CD workflows
├── Directory.Build.props       # Common .NET build properties
└── Garage.slnx                 # Solution file
```

## Build and Development Commands

### .NET Backend

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Build in Release mode
dotnet build --configuration Release

# Run the application (from AppHost)
cd src/Garage.AppHost
dotnet run

# Format code
dotnet format

# Verify formatting
dotnet format --verify-no-changes
```

### React Frontend

```bash
cd src/Garage.Web

# Install dependencies
npm install

# Development server
npm run dev

# Build for production
npm run build

# Lint code
npm run lint

# Preview production build
npm run preview
```

### Go Feature Flags API

```bash
cd src/Garage.FeatureFlags

# Download dependencies
go mod download

# Build the application
go build -o flags-api .

# Run the application
go run .

# Format code
go fmt ./...

# Run linter (requires golangci-lint)
golangci-lint run
```

## Coding Standards and Conventions

### .NET Code

- **Language Version**: Latest C# features enabled
- **Nullable Reference Types**: Enabled throughout the project
- **Warnings as Errors**: All warnings are treated as errors (`TreatWarningsAsErrors=true`)
- **Implicit Usings**: Enabled for cleaner code
- **Formatting**: Follow `dotnet format` conventions as defined in `.editorconfig`

### TypeScript/React Code

- **Linting**: ESLint with TypeScript parser
- **Type Safety**: TypeScript strict mode (~5.9.3)
- **React Version**: 19.2 with hooks
- **Style**: Follow ESLint rules in the configuration
- **Module System**: ES modules

### Go Code

- **Go Version**: 1.25 or later
- **Formatting**: Use `go fmt` for code formatting
- **Error Handling**: Follow Go idioms for error handling
- **Telemetry**: OpenTelemetry integration for tracing, metrics, and logging
- **HTTP Framework**: Standard library `net/http` with `otelhttp` instrumentation
- **Feature Flags**: OpenFeature Go SDK with OFREP provider

## Important Patterns and Practices

### Feature Flags

When working with feature flags:
- Use OpenFeature SDK for both .NET and React
- Feature flag keys use kebab-case (e.g., `enable-database-winners`)
- Boolean flags for toggle features
- Integer flags for numeric values
- Targeting rules supported via flagd

Example feature flags in use:
- `enable-database-winners`: Toggle between database and JSON data sources
- `winners-count`: Control number of items displayed
- `enable-stats-header`: Show/hide statistics header
- `enable-tabs`: Enable tabbed interface
- `enable-preview-mode`: Comma-separated list of flags that can be dynamically updated

### Service Configuration

- Service defaults are in `Garage.ServiceDefaults` for shared configuration
- Use .NET Aspire for service orchestration and discovery
- All services include OpenTelemetry instrumentation

### Database Access

- Use Entity Framework Core with PostgreSQL provider
- Database context should be registered via Aspire integration
- Migrations are managed through EF Core

## Testing

Currently, there are no test projects in the solution. When adding tests:
- Follow xUnit or NUnit conventions
- Create test projects with `*.Tests.csproj` naming
- Include both unit tests and integration tests where appropriate
- For React components, consider adding Vitest or Jest

## CI/CD

### GitHub Actions Workflows

1. **CI Build** (`.github/workflows/ci.yml`)
   - Runs on push to main and all PRs
   - Builds with `dotnet build --configuration Release`
   - Tests are commented out (add when test projects exist)

2. **Format Check** (`.github/workflows/format.yml`)
   - Runs on push to main and PRs to main
   - Verifies code formatting with `dotnet format --verify-no-changes`

## Dependencies and Security

- Use Dependabot for dependency updates (configured in `.github/dependabot.yml`)
- Keep .NET SDK at version 10.0.100 or later (see `global.json`)
- Review and update NuGet and npm packages regularly
- Follow security best practices for feature flag configuration

## Documentation

- Main README in root provides project overview and quick start
- Component-specific README in `src/Garage.Web/README.md`
- Update documentation when adding new features or changing architecture
- Include inline code comments for complex business logic

## Common Tasks

### Adding a New Feature Flag

1. Define the flag in flagd configuration
2. Add flag evaluation in service defaults
3. Use in .NET: `await _featureClient.GetBooleanValueAsync("flag-name", false)`
4. Use in React: `const flagValue = useFlag('flag-name')`

### Adding a New API Endpoint

1. Add to `Garage.ApiService` controllers
2. Add DTO if needed in `Garage.Shared`
3. Update OpenAPI documentation
4. Add caching if appropriate

### Database Schema Changes

1. Update Entity Framework models
2. Create migration: `dotnet ef migrations add MigrationName`
3. Update database seeder if needed
4. Test migration rollback

## Notes for Copilot

- This is a demonstration/sample application showcasing OpenFeature capabilities
- Code quality is important - maintain clean, maintainable code
- Feature flags are central to this project - understand OFREP concepts
- The application uses .NET Aspire for cloud-ready development
- Both backend and frontend use OpenFeature for consistent feature flag experience
- The Go Feature Flags API (`Garage.FeatureFlags`) provides dynamic flag targeting management
  - Exposes REST endpoints for getting and updating flag targeting rules
  - Uses OpenFeature Go SDK with OFREP provider for flag evaluation
  - Includes full OpenTelemetry instrumentation (traces, metrics, logs)
  - Reads/writes to the flagd.json configuration file
