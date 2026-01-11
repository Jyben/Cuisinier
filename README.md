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

#### Pour Cuisinier.AppHost (orchestration)
```bash
cd Cuisinier.AppHost
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key"
```

#### Pour Cuisinier.Api (API)
```bash
cd Cuisinier.Api
# SMTP Configuration
dotnet user-secrets set "Smtp:Host" "smtp"
dotnet user-secrets set "Smtp:Port" "0"
dotnet user-secrets set "Smtp:Username" "your-email@example.com"
dotnet user-secrets set "Smtp:Password" "your-password"
dotnet user-secrets set "Smtp:FromEmail" "your-email@example.com"

# JWT Configuration
dotnet user-secrets set "Jwt:SecretKey" "your-secret-key-at-least-32-characters-long"
```

Pour plus de détails, consultez [SECRETS.md](SECRETS.md).

### 2. Launch the application

```bash
dotnet run --project Cuisinier.AppHost
```