using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.Api.Middleware;

/// <summary>
/// Middleware for API key authentication
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context, IAuditLoggingService auditService)
    {
        // Skip authentication for health check endpoints
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Skip authentication for SignalR hub negotiation
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _next(context);
            return;
        }

        // Check if API key authentication is enabled
        var authEnabled = _configuration.GetValue<bool>("Security:ApiKeyAuthenticationEnabled", false);
        if (!authEnabled)
        {
            await _next(context);
            return;
        }

        // Extract API key from header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning("API key missing from request: {Path}", context.Request.Path);
            await auditService.LogSecurityEventAsync(
                "UnauthorizedAccess",
                $"Missing API key for {context.Request.Path}",
                null,
                context.Connection.RemoteIpAddress?.ToString());

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        // Validate API key
        var validApiKeys = _configuration.GetSection("Security:ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        if (!validApiKeys.Contains(extractedApiKey.ToString()))
        {
            _logger.LogWarning("Invalid API key used for request: {Path}", context.Request.Path);
            await auditService.LogSecurityEventAsync(
                "InvalidApiKey",
                $"Invalid API key attempt for {context.Request.Path}",
                null,
                context.Connection.RemoteIpAddress?.ToString());

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        // API key is valid, continue processing
        _logger.LogDebug("API key validated successfully for {Path}", context.Request.Path);
        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering API key authentication middleware
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
