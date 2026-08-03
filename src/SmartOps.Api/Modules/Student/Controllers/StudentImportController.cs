using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.AcademicYear;
using SmartOps.Application.Modules.BulkImport;
using SmartOps.Application.Modules.Student.Import;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Student.Controllers;

[ApiController]
[Route("api/students/import")]
[Authorize]
public sealed class StudentImportController(
    IStudentImportService studentImportService,
    IAcademicYearContext academicYearContext,
    ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet("template")]
    [Authorize(Policy = MenuPolicies.StudentBulkImport.View)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        byte[] bytes = await studentImportService.BuildTemplateAsync(cancellationToken).ConfigureAwait(false);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "student-import-template.xlsx");
    }

    [HttpPost("validate")]
    [Authorize(Policy = MenuPolicies.StudentBulkImport.Add)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StudentImportValidateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<StudentImportValidateResultDto>> Validate(
        IFormFile file,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Excel file is required." });
        }

        if (!IsExcel(file.FileName))
        {
            return BadRequest(new { message = "Only .xlsx Excel files are supported." });
        }

        if (!TryResolveAcademicYearId(academicYearId, out Guid yearId))
        {
            return BadRequest(new { message = "Academic year is required. Select a year in Settings / header." });
        }

        await using var stream = file.OpenReadStream();
        var result = await studentImportService
            .ValidateAsync(stream, yearId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("commit")]
    [Authorize(Policy = MenuPolicies.StudentBulkImport.Add)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StudentImportCommitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<StudentImportCommitResultDto>> Commit(
        IFormFile file,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Excel file is required." });
        }

        if (!IsExcel(file.FileName))
        {
            return BadRequest(new { message = "Only .xlsx Excel files are supported." });
        }

        if (!TryResolveAcademicYearId(academicYearId, out Guid yearId))
        {
            return BadRequest(new { message = "Academic year is required. Select a year in Settings / header." });
        }

        string? schoolRaw = tenantProvider.GetCurrentSchoolId();
        if (string.IsNullOrWhiteSpace(schoolRaw) || !Guid.TryParse(schoolRaw, out Guid schoolId))
        {
            return BadRequest(new { message = "School context is required." });
        }

        await using var stream = file.OpenReadStream();
        var result = await studentImportService
            .CommitAsync(stream, yearId, schoolId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    private bool TryResolveAcademicYearId(Guid? academicYearId, out Guid yearId)
    {
        yearId = academicYearId is { } id && id != Guid.Empty
            ? id
            : academicYearContext.EffectiveAcademicYearId ?? Guid.Empty;
        return yearId != Guid.Empty;
    }

    private static bool IsExcel(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
}
