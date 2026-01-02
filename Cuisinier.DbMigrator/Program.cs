using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cuisinier.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Add DbContext
builder.Services.AddDbContext<CuisinierDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

var host = builder.Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var logger = services.GetRequiredService<ILogger<Program>>();
var dbContext = services.GetRequiredService<CuisinierDbContext>();

try
{
    logger.LogInformation("Démarrage de l'application de migration de base de données...");
    
    logger.LogInformation("Application des migrations...");
    await dbContext.Database.MigrateAsync();
    
    logger.LogInformation("Migrations appliquées avec succès.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Une erreur s'est produite lors de l'application des migrations.");
    throw;
}
