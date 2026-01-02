using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FluentValidation;
using FluentValidation.AspNetCore;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Api.Endpoints;
using Cuisinier.Api.JsonConverters;
using Cuisinier.Api.Hubs;
using Cuisinier.Api.Services;
using Cuisinier.Api.Middleware;
using Cuisinier.Api.HealthChecks;
using Cuisinier.Core.Validators;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<CuisinierDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

// Register services
var openAIApiKey = builder.Configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
builder.Services.AddSingleton<IOpenAIService>(sp => 
    new OpenAIService(openAIApiKey, sp.GetRequiredService<ILogger<OpenAIService>>()));

builder.Services.AddScoped<Cuisinier.Infrastructure.Services.IRecipeService, Cuisinier.Infrastructure.Services.RecipeService>();
builder.Services.AddScoped<BackgroundRecipeService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IRecipeQueryService, RecipeQueryService>();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<MenuParametersValidator>();

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add SignalR
builder.Services.AddSignalR();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        name: "database",
        tags: new[] { "ready", "liveness" })
    .AddCheck<OpenAIServiceHealthCheck>(
        name: "openai",
        tags: new[] { "ready" });

// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new TimeSpanConverter());
    options.SerializerOptions.Converters.Add(new NullableTimeSpanConverter());
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name 
                ?? context.Connection.RemoteIpAddress?.ToString() 
                ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many requests. Please try again later." },
            cancellationToken);
    };
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("https://localhost:5001", "http://localhost:5000", "https://localhost:7092", "http://localhost:5079")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>();

            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins must be configured in production. " +
                    "Please add the Cors:AllowedOrigins array to your appsettings.Production.json");
            }

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Correlation ID Middleware (must be early in pipeline, before exception handling)
app.UseMiddleware<CorrelationIdMiddleware>();

// Global Exception Handling Middleware (must be early in pipeline)
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();

// Health Checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("liveness")
});

// Map SignalR hub
app.MapHub<RecipeHub>("/recipeHub");

// Map endpoints
app.MapMenuEndpoints();
app.MapRecipeEndpoints();
app.MapShoppingListEndpoints();

app.Run();
