using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using FluentValidation;
using FluentValidation.AspNetCore;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Api.Endpoints;
using Cuisinier.Api.JsonConverters;
using Cuisinier.Api.Hubs;
using Cuisinier.Api.Services;
using Cuisinier.Api.Middleware;
using Cuisinier.Api.HealthChecks;
using Cuisinier.Shared.Validators;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<CuisinierDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;

    // Token settings
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultProvider;
    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
})
.AddEntityFrameworkStores<CuisinierDbContext>()
.AddDefaultTokenProviders();

// Add JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not configured");
if (jwtSecretKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters long");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CuisinierApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CuisinierApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure OpenAI Service Options
builder.Services.Configure<Cuisinier.Infrastructure.Services.Options.OpenAIServiceOptions>(
    builder.Configuration.GetSection(Cuisinier.Infrastructure.Services.Options.OpenAIServiceOptions.SectionName));

// Register OpenAI Service dependencies
builder.Services.AddSingleton<Cuisinier.Infrastructure.Services.Helpers.TimeSpanParser>();
builder.Services.AddSingleton<Cuisinier.Infrastructure.Services.Mappers.OpenAIResponseMapper>();

// Register OpenAI Service
var openAIApiKey = builder.Configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
builder.Services.AddSingleton<IOpenAIService>(sp => 
    new OpenAIService(
        openAIApiKey, 
        sp.GetRequiredService<ILogger<OpenAIService>>(),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Cuisinier.Infrastructure.Services.Options.OpenAIServiceOptions>>(),
        sp.GetRequiredService<Cuisinier.Infrastructure.Services.Mappers.OpenAIResponseMapper>()));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<Cuisinier.Infrastructure.Services.IRecipeService, Cuisinier.Infrastructure.Services.RecipeService>();
builder.Services.AddScoped<BackgroundRecipeService>();
builder.Services.AddScoped<BackgroundMenuService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IRecipeQueryService, RecipeQueryService>();
builder.Services.AddScoped<IFamilyLinkService, FamilyLinkService>();
builder.Services.AddScoped<IUserAccessService, UserAccessService>();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<MenuParametersValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

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

    // Strict rate limiting policy for authentication endpoints
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,           // 5 tentatives
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

// Initialize roles (only if database is migrated)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Check if database exists and migrations are applied
        if (await dbContext.Database.CanConnectAsync())
        {
            // Check if AspNetRoles table exists by trying to query it
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (!pendingMigrations.Any())
            {
                // Create "User" role if it doesn't exist
                if (!await roleManager.RoleExistsAsync("User"))
                {
                    await roleManager.CreateAsync(new IdentityRole("User"));
                }
            }
        }
    }
    catch (Exception ex)
    {
        // Log error but don't fail application startup
        // This allows the application to start even if migrations haven't been applied yet
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not initialize roles. Make sure migrations have been applied. Error: {Message}", ex.Message);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Correlation ID Middleware (must be early in pipeline, before exception handling)
app.UseMiddleware<CorrelationIdMiddleware>();

// Content Language Middleware (set French language for all responses)
app.UseMiddleware<ContentLanguageMiddleware>();

// Global Exception Handling Middleware (must be early in pipeline)
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Prometheus Metrics
app.UseMetricServer();
app.UseHttpMetrics();

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
app.MapAuthEndpoints();
app.MapMenuEndpoints();
app.MapRecipeEndpoints();
app.MapShoppingListEndpoints();
app.MapFavoriteEndpoints();
app.MapDishEndpoints();
app.MapFamilyLinkEndpoints();

app.Run();
