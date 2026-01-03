namespace Cuisinier.Api.Middleware;

public class ContentLanguageMiddleware
{
    private readonly RequestDelegate _next;

    public ContentLanguageMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Set Content-Language header to French for all responses
        context.Response.Headers["Content-Language"] = "fr";
        
        await _next(context);
    }
}
