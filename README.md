# OpenFeature .NET OFREP Demo: Le Mans Winners Management System

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Python 3.14](https://img.shields.io/badge/Python-3.14-3776AB?logo=python)](https://python.org/)
[![Go 1.25](https://img.shields.io/badge/Go-1.25-00ADD8?logo=go)](https://go.dev/)
[![Aspire](https://img.shields.io/badge/Aspire-Enabled-purple)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![OpenFeature](https://img.shields.io/badge/OpenFeature-Ready-green)](https://openfeature.dev/)
[![OFREP](https://img.shields.io/badge/OFREP-Enabled-blue)](https://openfeature.dev/specification/ofrep)

A demonstration application showcasing **OpenFeature Remote Evaluation Protocol (OFREP)** capabilities in a polyglot environment with .NET, Python, Go, and React. This application manages a collection of Le Mans winner cars and includes an AI-powered chatbot.

## What This Demonstrates

This demo showcases how to implement feature flags using **OpenFeature** and the **OFREP (OpenFeature Remote Evaluation Protocol)** in a full-stack polyglot application. Key features include:

- **OFREP Integration**: Remote feature flag evaluation using the standardized protocol across .NET, Python, and Go
- **OpenFeature SDK**: Industry-standard feature flagging for .NET backend, Python service, and React frontend
- **flagd Provider**: Using flagd as the feature flag evaluation engine with OFREP
- **Dynamic Configuration**: Real-time feature flag updates without redeployment
- **Full-Stack Implementation**: Feature flags working seamlessly across React UI, .NET API, and Python services
- **Kill Switches**: Safely toggle features in production environments
- **GitHub Models Integration**: AI-powered chatbot using GPT-4o via GitHub Models
- **GitHub Repository Prompts**: Dynamic prompt selection using `.prompt.yml` files

## Architecture

### Components

- **Garage.Web**: React + Vite frontend for managing car collections with floating chatbot UI
- **Garage.ApiService**: REST API for car data with Entity Framework Core
- **Garage.ChatService**: Python FastAPI service for AI chatbot using GitHub Models
- **Garage.FeatureFlags**: Go API for managing feature flag targeting rules
- **Garage.ServiceDefaults**: Shared services including feature flag implementations
- **Garage.Shared**: Common models and DTOs
- **Garage.AppHost**: .NET Aspire orchestration and service discovery

### Infrastructure

- **PostgreSQL**: Database for storing car collection data
- **Redis**: Caching layer for improved performance
- **flagd**: OpenFeature-compliant feature flag evaluation engine
- **GitHub Models**: AI model provider for chatbot functionality

## Telemetry Support

This application includes comprehensive telemetry support through .NET Aspire:

- **Distributed Tracing**: Track requests across all services (.NET, Python, Go)
- **Metrics Collection**: Monitor application performance, feature flag usage, and chat request counts
- **Structured Logging**: Centralized log aggregation with trace correlation

> **Note**: All services export telemetry via OTLP to the Aspire dashboard.

## Feature Flags Included

The demo demonstrates these feature flags:

| Flag                       | Type     | Purpose                                   | Default       |
| -------------------------- | -------- | ----------------------------------------- | ------------- |
| `enable-database-winners`  | `bool`   | Toggle data source (DB vs JSON)           | `true`        |
| `winners-count`            | `int`    | Control number of winners shown           | `100`         |
| `enable-stats-header`      | `bool`   | Show/hide statistics header               | `true`        |
| `enable-tabs`              | `bool`   | Enable tabbed interface (with targeting)  | `false`       |
| `enable-preview-mode`      | `string` | Comma-separated list of editable flags    | `""`          |
| `enable-chatbot`           | `bool`   | Show/hide AI chatbot (with targeting)     | `false`       |
| `prompt-file`              | `string` | Select chatbot prompt style               | `"expert"`    |

### Chatbot Prompt Styles

The chatbot supports multiple prompt styles via GitHub Repository Prompts (`.prompt.yml` files):

- **expert**: Detailed Le Mans racing historian with comprehensive knowledge
- **casual**: Friendly enthusiast for casual conversation
- **brief**: Quick facts with concise responses
- **unreliable**: Confidently incorrect information (for A/B testing demos)

## Requirements

### Prerequisites

- .NET 10.0 SDK or later
- Python 3.12 or later
- Go 1.25 or later (for Feature Flags API)
- Visual Studio, Visual Studio Code with C# extension or JetBrains Rider
- Git for version control
- Docker Desktop (for containerized dependencies)
- GitHub PAT with access to GitHub Models (for chatbot functionality)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/open-feature/openfeature-dotnet-workshop.git
cd openfeature-dotnet-workshop
```

### 2. Configure GitHub Token (for Chatbot)

```bash
cd src/Garage.AppHost
dotnet user-secrets set "Parameters:github-token" "<your-github-pat>"
```

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Run with .NET Aspire

```bash
aspire run
```

### 5. Access the Application

- Web Frontend: https://localhost:7070
- API Service: https://localhost:7071
- Aspire Dashboard: https://localhost:15888

The application will start with flagd running as a container, providing OFREP endpoints for the React frontend, .NET API service, and Python chatbot to consume feature flags.

## Python Chat Service

The Python chat service (`Garage.ChatService`) provides an AI-powered chatbot for Le Mans racing questions:

- **Framework**: FastAPI with Uvicorn
- **AI Provider**: GitHub Models (GPT-4o)
- **Feature Flags**: OpenFeature with OFREP provider
- **Telemetry**: Full OpenTelemetry integration (traces, metrics, logs)
- **Prompts**: GitHub Repository Prompts format (`.prompt.yml`)

### API Endpoints

```
POST /chat
Request: { "message": "Who won Le Mans in 2023?", "userId": "user-123" }
Response: { "response": "...", "prompt_style": "expert" }

GET /health
Response: { "status": "healthy" }
```

## Additional Resources

- [OpenFeature Documentation](https://docs.openfeature.dev/)
- [OFREP Specification](https://openfeature.dev/specification/ofrep)
- [flagd Documentation](https://flagd.dev/)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [GitHub Models Documentation](https://docs.github.com/en/github-models)
- [GitHub Repository Prompts](https://docs.github.com/en/github-models/use-github-models/storing-prompts-in-github-repositories)
- [Feature Flag Best Practices](https://martinfowler.com/articles/feature-toggles.html)

## License

This project is licensed under the [MIT License](LICENSE).
