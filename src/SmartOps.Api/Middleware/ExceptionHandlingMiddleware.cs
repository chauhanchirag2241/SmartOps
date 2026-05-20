using System.Text.Json;
using FluentValidation;
using SmartOps.Domain.Common;

namespace SmartOps.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (ValidationException validationException)
        {
            _logger.LogWarning(validationException, "Request validation failed.");
            await WriteJsonAsync(
                context,
                StatusCodes.Status400BadRequest,
                new
                {
                    error = "Validation failed.",
                    details = validationException.Errors.Select(e => e.ErrorMessage).ToArray()
                }).ConfigureAwait(false);
        }
        catch (ConcurrencyException concurrencyException)
        {
            _logger.LogWarning(concurrencyException, "Optimistic concurrency conflict.");
            await WriteJsonAsync(
                context,
                StatusCodes.Status409Conflict,
                new { error = concurrencyException.Message }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception.");
            await WriteJsonAsync(
                context,
                StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred." }).ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, object payload)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions)).ConfigureAwait(false);
    }
}
