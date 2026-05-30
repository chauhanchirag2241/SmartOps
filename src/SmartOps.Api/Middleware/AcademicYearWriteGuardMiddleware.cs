using SmartOps.Application.Modules.AcademicYear;

namespace SmartOps.Api.Middleware;

/// <summary>
/// Blocks create/update/delete when viewing a past academic year in the header.
/// </summary>
public sealed class AcademicYearWriteGuardMiddleware
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
    };

    private readonly RequestDelegate _next;

    public AcademicYearWriteGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAcademicYearContext academicYearContext)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && MutatingMethods.Contains(context.Request.Method)
            && !IsExemptPath(context.Request.Path)
            && academicYearContext.IsResolved
            && academicYearContext.IsReadOnlyAcademicYear)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Changes are not allowed for past academic years. Select the current or an upcoming year in the header to add, edit, or delete.",
                code = "ACADEMIC_YEAR_READ_ONLY",
            }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsExemptPath(PathString path)
    {
        string value = path.Value?.ToLowerInvariant() ?? string.Empty;
        return value.StartsWith("/api/auth", StringComparison.Ordinal)
            || value.StartsWith("/api/academicyears", StringComparison.Ordinal)
            || value.StartsWith("/swagger", StringComparison.Ordinal)
            || value.StartsWith("/health", StringComparison.Ordinal);
    }
}
