# Cuisinier

Weekly menu generation application with detailed recipes, using OpenAI for content generation.

## Project Structure

- **Cuisinier.AppHost** : Aspire project for orchestration (development)
- **Cuisinier.Api** : .NET REST API
- **Cuisinier.App** : Blazor WebAssembly application
- **Cuisinier.Core** : Domain models and DTOs
- **Cuisinier.Infrastructure** : Data access (Entity Framework Core)
- **Cuisinier.DbMigrator** : Database migration

## Development Prerequisites

- .NET 10.0 SDK
- Docker Desktop (for SQL Server)
- OpenAI API key

## Local Configuration

### 1. Configure secrets

```bash
cd Cuisinier.AppHost
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key"
```

### 2. Launch the application

```bash
dotnet run --project Cuisinier.AppHost
```