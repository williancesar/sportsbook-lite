using Orleans.Runtime;
using Serilog.Context;

namespace SportsbookLite.Api.Middleware;

/// <summary>
/// Middleware that adds correlation ID to all requests for distributed tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from request header, or generate a new one
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();

        // Add correlation ID to response headers
        context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);

        // Store correlation ID in HttpContext for later use
        context.Items["CorrelationId"] = correlationId;

        // Add correlation ID to Orleans RequestContext for grain calls
        RequestContext.Set("CorrelationId", correlationId);
        RequestContext.Set("RequestId", context.TraceIdentifier);
        RequestContext.Set("CallerId", context.Request.Path);

        // Add correlation ID to Serilog context for all logs in this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        using (LogContext.PushProperty("ClientIP", context.Connection.RemoteIpAddress?.ToString()))
        {
            _logger.LogInformation(
                "Request started: {Method} {Path} with CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            try
            {
                await _next(context);

                _logger.LogInformation(
                    "Request completed: {Method} {Path} with CorrelationId: {CorrelationId} - Status: {StatusCode}",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId,
                    context.Response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Request failed: {Method} {Path} with CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);
                throw;
            }
        }
    }
}

/// <summary>
/// Extension methods for adding correlation ID middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}