using System.Diagnostics;

namespace MiniApy.Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    private const string CorrelationHeader =
        "X-Correlation-ID";

    public async Task InvokeAsync(
        HttpContext httpContext)
    {
        var correlationId =
            GetOrCreateCorrelationId(httpContext);

        httpContext.TraceIdentifier = correlationId;

        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[
                CorrelationHeader] = correlationId;

            return Task.CompletedTask;
        });

        using var loggingScope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["TraceId"] = correlationId
            });

        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "HTTP request started. " +
            "Method: {Method}, Path: {Path}, TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            correlationId);

        try
        {
            await next(httpContext);
        }
        finally
        {
            stopwatch.Stop();

            logger.LogInformation(
                "HTTP request completed. " +
                "Method: {Method}, Path: {Path}, " +
                "StatusCode: {StatusCode}, " +
                "DurationMs: {DurationMs}, TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                correlationId);
        }
    }

    private static string GetOrCreateCorrelationId(
        HttpContext httpContext)
    {
        var suppliedCorrelationId =
            httpContext.Request.Headers[
                CorrelationHeader].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(
                suppliedCorrelationId) &&
            suppliedCorrelationId.Length <= 128)
        {
            return suppliedCorrelationId;
        }

        return Guid.NewGuid().ToString("N");
    }
}