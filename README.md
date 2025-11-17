# OpenFeature .NET OFREP Demo: Le Mans Winners Management System

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Aspire](https://img.shields.io/badge/Aspire-Enabled-purple)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![OpenFeature](https://img.shields.io/badge/OpenFeature-Ready-green)](https://openfeature.dev/)
[![OFREP](https://img.shields.io/badge/OFREP-Enabled-blue)](https://openfeature.dev/specification/ofrep)

A demonstration application showcasing **OpenFeature Remote Evaluation Protocol (OFREP)** capabilities in a .NET environment with React frontend. This application manages a collection of Le Mans winner cars.

## What This Demonstrates

This demo showcases how to implement feature flags using **OpenFeature** and the **OFREP (OpenFeature Remote Evaluation Protocol)** in a full-stack .NET application with React frontend. Key features include:

- **OFREP Integration**: Remote feature flag evaluation using the standardized protocol
- **OpenFeature SDK**: Industry-standard feature flagging for both .NET backend and React frontend
- **flagd Provider**: Using flagd as the feature flag evaluation engine with OFREP
- **Dynamic Configuration**: Real-time feature flag updates without redeployment
- **Full-Stack Implementation**: Feature flags working seamlessly across React UI and .NET API
- **Kill Switches**: Safely toggle features in production environments

## Architecture

### Components

- **Garage.Web**: React + Vite frontend for managing car collections
- **Garage.ApiService**: REST API for car data with Entity Framework Core
- **Garage.ServiceDefaults**: Shared services including feature flag implementations
- **Garage.Shared**: Common models and DTOs
- **Garage.AppHost**: .NET Aspire orchestration and service discovery

### Infrastructure

- **PostgreSQL**: Database for storing car collection data
- **Redis**: Caching layer for improved performance
- **flagd**: OpenFeature-compliant feature flag evaluation engine

## Telemetry Support

This application includes comprehensive telemetry support through .NET Aspire:

- **Distributed Tracing**: Track requests across services
- **Metrics Collection**: Monitor application performance and feature flag usage
- **Logging**: Centralized log aggregation

> **Note**: When deployed, telemetry will work for the API service but not for the web frontend.

## Feature Flags Included

The demo demonstrates these feature flags:

| Flag                       | Type   | Purpose                         | Default       |
| -------------------------- | ------ | ------------------------------- | ------------- |
| `enable-database-winners`  | `bool` | Toggle data source (DB vs JSON) | `true`        |
| `winners-count`            | `int`  | Control number of winners shown | `100`         |
| `enable-stats-header`      | `bool` | Show/hide statistics header     | `true`        |
| `enable-tabs`              | `bool` | Enable tabbed interface (with targeting) | `false` |

## Requirements

### Prerequisites

- .NET 10.0 SDK or later
- Visual Studio, Visual Studio Code with C# extension or JetBrains Rider
- Git for version control
- Docker Desktop (for containerized dependencies)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/open-feature/openfeature-dotnet-workshop.git
cd openfeature-dotnet-workshop
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Run with .NET Aspire

```bash
cd src/Garage.AppHost
dotnet run
```

### 4. Access the Application

- Web Frontend: https://localhost:7070
- API Service: https://localhost:7071
- Aspire Dashboard: https://localhost:15888

The application will start with flagd running as a container, providing OFREP endpoints for both the React frontend and .NET API service to consume feature flags.

## Additional Resources

- [OpenFeature Documentation](https://docs.openfeature.dev/)
- [OFREP Specification](https://openfeature.dev/specification/ofrep)
- [flagd Documentation](https://flagd.dev/)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Feature Flag Best Practices](https://martinfowler.com/articles/feature-toggles.html)

## License

This project is licensed under the [MIT License](LICENSE).
