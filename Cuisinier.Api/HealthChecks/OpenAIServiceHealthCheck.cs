using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cuisinier.Api.HealthChecks;

public class OpenAIServiceHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIServiceHealthCheck> _logger;

    public OpenAIServiceHealthCheck(
        IConfiguration configuration,
        ILogger<OpenAIServiceHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key is not configured");
                return Task.FromResult(HealthCheckResult.Unhealthy("OpenAI API key is not configured"));
            }

            return Task.FromResult(HealthCheckResult.Healthy("OpenAI service is configured"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI service health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("OpenAI service is unavailable", ex));
        }
    }
}

