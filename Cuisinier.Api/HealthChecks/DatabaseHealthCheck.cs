using Microsoft.Extensions.Diagnostics.HealthChecks;
using Cuisinier.Infrastructure.Data;

namespace Cuisinier.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly CuisinierDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        CuisinierDbContext context,
        ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database is available");
            }
            
            return HealthCheckResult.Unhealthy("Database is unavailable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database is unavailable", ex);
        }
    }
}

