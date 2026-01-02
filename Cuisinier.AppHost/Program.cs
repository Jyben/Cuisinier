var builder = DistributedApplication.CreateBuilder(args);

// SQL Server avec volume persistant pour conserver les données
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithDataVolume("cuisinier-db")
    .AddDatabase("CuisinierDb");

// API
var api = builder.AddProject<Projects.Cuisinier_Api>("api")
    .WithReference(sqlServer)
    .WithEnvironment("OpenAI__ApiKey", builder.Configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured. Configurez-la avec: dotnet user-secrets set \"OpenAI:ApiKey\" \"votre-cle\" --project Cuisinier.AppHost"))
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqlServer);

// Blazor App
var app = builder.AddProject<Projects.Cuisinier_App>("app")
    .WithReference(api);

// DbMigrator
var dbMigrator = builder.AddProject<Projects.Cuisinier_DbMigrator>("dbmigrator")
    .WithReference(sqlServer)
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqlServer);

builder.Build().Run();

