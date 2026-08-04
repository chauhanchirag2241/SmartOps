using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.BulkImport;
using SmartOps.Application.Modules.Employee.Import;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Employee.Controllers;

[ApiController]
[Route("api/employees/import")]
[Authorize]
public sealed class EmployeeImportController(
    IEmployeeImportService employeeImportService,
    ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet("template")]
    [Authorize(Policy = MenuPolicies.EmployeeBulkImport.View)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        byte[] bytes = await employeeImportService.BuildTemplateAsync(cancellationToken).ConfigureAwait(false);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "employee-import-template.xlsx");
    }

    [HttpPost("validate")]
    [Authorize(Policy = MenuPolicies.EmployeeBulkImport.Add)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EmployeeImportValidateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<EmployeeImportValidateResultDto>> Validate(
        IFormFile file,
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

        await using var stream = file.OpenReadStream();
        var result = await employeeImportService
            .ValidateAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("commit")]
    [Authorize(Policy = MenuPolicies.EmployeeBulkImport.Add)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EmployeeImportCommitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<EmployeeImportCommitResultDto>> Commit(
        IFormFile file,
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

        string? schoolRaw = tenantProvider.GetCurrentSchoolId();
        if (string.IsNullOrWhiteSpace(schoolRaw) || !Guid.TryParse(schoolRaw, out Guid schoolId))
        {
            return BadRequest(new { message = "School context is required." });
        }

        await using var stream = file.OpenReadStream();
        var result = await employeeImportService
            .CommitAsync(stream, schoolId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    private static bool IsExcel(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
}
