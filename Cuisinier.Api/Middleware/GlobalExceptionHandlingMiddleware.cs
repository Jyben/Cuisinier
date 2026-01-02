using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Cuisinier.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var response = context.Response;

        var errorResponse = new ErrorResponse
        {
            StatusCode = response.StatusCode,
            Message = "An error occurred while processing your request.",
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ArgumentNullException nullEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = $"Required parameter is missing: {nullEx.ParamName}";
                _logger.LogWarning(nullEx, "Null argument: {ParamName}", nullEx.ParamName);
                break;

            case ArgumentException argEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = argEx.Message;
                _logger.LogWarning(argEx, "Invalid argument: {Message}", argEx.Message);
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Message = "The requested resource was not found.";
                _logger.LogWarning(exception, "Resource not found");
                break;

            case DbUpdateConcurrencyException:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Message = "The resource has been modified by another operation.";
                _logger.LogWarning(exception, "Concurrency conflict");
                break;

            case DbUpdateException dbEx:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Message = "A database error occurred while processing your request.";
                _logger.LogError(dbEx, "Database error: {Message}", dbEx.Message);
                
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = dbEx.InnerException?.Message;
                }
                break;

            case InvalidOperationException invalidOp:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = invalidOp.Message;
                _logger.LogWarning(invalidOp, "Invalid operation: {Message}", invalidOp.Message);
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Message = "You are not authorized to perform this action.";
                _logger.LogWarning(exception, "Unauthorized access attempt");
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Message = "An internal server error occurred.";
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = exception.ToString();
                }
                break;
        }

        errorResponse.StatusCode = response.StatusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsJsonAsync(errorResponse, jsonOptions);
    }
}

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

