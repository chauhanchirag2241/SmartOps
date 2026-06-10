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
        catch (InvalidOperationException invalidOpException)
        {
            _logger.LogWarning(invalidOpException, "Invalid operation.");
            await WriteJsonAsync(
                context,
                StatusCodes.Status400BadRequest,
                new { error = invalidOpException.Message, message = invalidOpException.Message }).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsUniqueViolation(exception, out string message))
        {
            _logger.LogWarning(exception, "Unique constraint violation.");
            await WriteJsonAsync(
                context,
                StatusCodes.Status409Conflict,
                new { error = message, message }).ConfigureAwait(false);
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

    private static bool IsUniqueViolation(Exception exception, out string message)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName != "Npgsql.PostgresException")
            {
                continue;
            }

            string? sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (sqlState != "23505")
            {
                continue;
            }

            string? constraint = current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string;
            message = constraint?.Contains("admission", StringComparison.OrdinalIgnoreCase) == true
                ? "Admission number already exists."
                : "Duplicate value is not allowed.";
            return true;
        }

        message = string.Empty;
        return false;
    }
}
