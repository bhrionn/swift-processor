using System.Diagnostics;
using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.Api.Middleware;

/// <summary>
/// Middleware for collecting Prometheus metrics on API requests
/// </summary>
public class PrometheusMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApplicationPerformanceMonitoringService _apmService;
    private readonly ILogger<PrometheusMetricsMiddleware> _logger;

    public PrometheusMetricsMiddleware(
        RequestDelegate next,
        ApplicationPerformanceMonitoringService apmService,
        ILogger<PrometheusMetricsMiddleware> logger)
    {
        _next = next;
        _apmService = apmService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip metrics endpoint itself to avoid recursion
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var endpoint = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        using var activity = _apmService.StartApiRequestActivity(endpoint, method);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            _apmService.RecordApiRequest(endpoint, method, statusCode, durationSeconds);

            if (activity != null)
            {
                activity.SetTag("http.status_code", statusCode);
                activity.SetTag("http.duration_ms", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

/// <summary>
/// Extension methods for registering Prometheus metrics middleware
/// </summary>
public static class PrometheusMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PrometheusMetricsMiddleware>();
    }
}
