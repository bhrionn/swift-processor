namespace SwiftMessageProcessor.Api.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent clickjacking attacks
        headers.TryAdd("X-Frame-Options", "DENY");

        // Prevent MIME type sniffing
        headers.TryAdd("X-Content-Type-Options", "nosniff");

        // Enable XSS protection
        headers.TryAdd("X-XSS-Protection", "1; mode=block");

        // Content Security Policy
        headers.TryAdd("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' ws: wss:; " +
            "frame-ancestors 'none'");

        // Referrer Policy
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions Policy (formerly Feature Policy)
        headers.TryAdd("Permissions-Policy",
            "geolocation=(), " +
            "microphone=(), " +
            "camera=(), " +
            "payment=(), " +
            "usb=(), " +
            "magnetometer=(), " +
            "gyroscope=(), " +
            "accelerometer=()");

        // Strict Transport Security (HSTS) - only in production
        if (!context.Request.Host.Host.Contains("localhost"))
        {
            headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
        }

        _logger.LogDebug("Security headers added to response");
    }
}

/// <summary>
/// Extension methods for registering security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
