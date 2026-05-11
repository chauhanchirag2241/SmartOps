using System.Diagnostics;

namespace SmartOps.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string method = context.Request.Method;
        string path = context.Request.Path.Value ?? string.Empty;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
