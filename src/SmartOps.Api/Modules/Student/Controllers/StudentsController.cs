using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Student;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Student.Controllers;

/// <summary>
/// Student CRUD + list. Pattern: thin controller → <see cref="IStudentRepository"/> (copy for other modules).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StudentsController(
    IStudentRepository studentRepository,
    IFeeStructureRepository feeStructureRepository,
    IClassFeeAmountRepository classFeeAmountRepository,
    IUserProvisioningService userProvisioning,
    IResourceAuthorizationService resourceAuthorization,
    ITenantProvider tenantProvider,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    private static readonly Regex AdmissionNoPattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>Generates the next roll number class-wise for a given academic year.</summary>
    [HttpGet("next-roll-number")]
    [Authorize(Policy = MenuPolicies.Students.Add)]
    public async Task<ActionResult<object>> GetNextRollNumber(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classId,
        CancellationToken cancellationToken)
    {
        int maxRoll = await studentRepository.GetMaxRollNumberAsync(academicYearId, classId, cancellationToken);
        return Ok(new { RollNumber = (maxRoll + 1).ToString() });
    }

    /// <summary>Create a student and related rows (parents, academics, etc.).</summary>

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Students.Add)]
    [ProducesResponseType(typeof(CreateStudentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateStudentResponse>> CreateStudent(
        [FromBody] CreateStudentDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Student data is required.");
        }

        ActionResult? admissionNoValidation = await ValidateAdmissionNoAsync(
                request.AdmissionNo,
                excludingStudentId: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (admissionNoValidation is not null)
        {
            return admissionNoValidation;
        }

        request.AdmissionNo = request.AdmissionNo!.Trim();
        var entity = request.ToEntity();

        foreach (var academic in entity.Academics)
        {
            if (academic.AcademicYearId == Guid.Empty)
            {
                continue;
            }

            var admissionFeeStructure = await feeStructureRepository
                .GetAdmissionVersionForYearAsync(academic.AcademicYearId, cancellationToken)
                .ConfigureAwait(false);
            if (admissionFeeStructure is null)
            {
                return BadRequest(new
                {
                    message = "Cannot admit student without a published fee structure. Publish the fee structure for this academic year first."
                });
            }

            if (academic.ClassId != Guid.Empty)
            {
                bool classHasConfiguredAmounts = await classFeeAmountRepository
                    .ClassHasConfiguredAmountsAsync(academic.ClassId, admissionFeeStructure.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (!classHasConfiguredAmounts)
                {
                    return BadRequest(new
                    {
                        message = "Set class-wise fee amounts for this class before admitting students."
                    });
                }
            }
        }

        if (request.FeeHeadSelections.Count > 0)
        {
            CreateStudentAcademicDto? admissionAcademic = request.Academics.FirstOrDefault(a => a.ClassId != Guid.Empty);
            if (admissionAcademic is not null && admissionAcademic.AcademicYearId != Guid.Empty)
            {
                var admissionVersion = await feeStructureRepository
                    .GetAdmissionVersionForYearAsync(admissionAcademic.AcademicYearId, cancellationToken)
                    .ConfigureAwait(false);
                if (admissionVersion is not null)
                {
                    IList<ClassFeeAmountRow> classAmounts = await classFeeAmountRepository
                        .GetAmountsByClassAsync(admissionAcademic.ClassId, admissionVersion.Id, cancellationToken)
                        .ConfigureAwait(false);
                    foreach (ClassFeeAmountRow mandatoryRow in classAmounts.Where(r => r.IsMandatory && r.Amount > 0))
                    {
                        CreateStudentFeeHeadSelectionDto? selection = request.FeeHeadSelections
                            .FirstOrDefault(s => s.FeeTypeId == mandatoryRow.FeeTypeId);
                        if (selection is { IsIncluded: false })
                        {
                            return BadRequest($"Mandatory fee '{mandatoryRow.FeeTypeName}' cannot be excluded.");
                        }
                    }
                }
            }
        }

        var studentId = await studentRepository.CreateStudentAsync(entity, cancellationToken).ConfigureAwait(false);

        if (entity.PortalAccess && TryGetSchoolId(out Guid schoolId))
        {
            Guid? userId = await userProvisioning
                .ProvisionStudentUserAsync(entity, schoolId, cancellationToken)
                .ConfigureAwait(false);

            if (userId.HasValue)
            {
                await studentRepository
                    .SetStudentUserIdAsync(studentId, userId.Value, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return Ok(new CreateStudentResponse("Student created successfully", studentId));
    }

    /// <summary>Paged list with optional search, sort, and status filter.</summary>
    [HttpGet]
    [Authorize(Policy = MenuPolicies.Students.ListForAttendanceOrModule)]
    [ProducesResponseType(typeof(PagedResult<StudentListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStudents(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] StudentFilter filter = StudentFilter.Active,
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid[]? classIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await studentRepository
            .GetAllStudentsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, classId, classIds, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>Full student graph by id (active only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Students.View)]
    [ProducesResponseType(typeof(StudentEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentEntity>> GetStudentById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorization.CanAccessStudentAsync(id, AccessLevel.View, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        var student = await studentRepository.GetStudentByIdAsync(id, cancellationToken, includeInactive: true).ConfigureAwait(false);
        return student is null ? NotFound() : Ok(student);
    }

    /// <summary>Replace student aggregate (body <see cref="StudentEntity.Id"/> must match route).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Students.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStudent(Guid id, [FromBody] StudentEntity student, CancellationToken cancellationToken)
    {
        if (id != student.Id)
        {
            return BadRequest("Route id and payload id must match.");
        }

        ActionResult? admissionNoValidation = await ValidateAdmissionNoAsync(
                student.AdmissionNo,
                excludingStudentId: id,
                cancellationToken)
            .ConfigureAwait(false);
        if (admissionNoValidation is not null)
        {
            return admissionNoValidation;
        }

        student.AdmissionNo = student.AdmissionNo!.Trim();

        if (!await resourceAuthorization.CanAccessStudentAsync(id, AccessLevel.Edit, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        await studentRepository.UpdateStudentAsync(student, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Soft-delete student and related rows.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Students.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteStudent(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorization.CanAccessStudentAsync(id, AccessLevel.Delete, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        await studentRepository.DeleteStudentAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }

    private async Task<ActionResult?> ValidateAdmissionNoAsync(
        string? admissionNo,
        Guid? excludingStudentId,
        CancellationToken cancellationToken)
    {
        string normalized = (admissionNo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest(new { message = "Admission number is required." });
        }

        if (!AdmissionNoPattern.IsMatch(normalized))
        {
            return BadRequest(new { message = "Admission number can contain only letters, numbers, hyphen (-), and underscore (_)." });
        }

        bool duplicate = await studentRepository
            .AdmissionNoExistsAsync(normalized, excludingStudentId, cancellationToken)
            .ConfigureAwait(false);
        return duplicate ? Conflict(new { message = "Admission number already exists." }) : null;
    }

    /// <summary>
    /// Returns paginated change history for a specific student.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableStudents, id, page, pageSize, cancellationToken);

        return Ok(result);
    }

    /// <summary>Checks whether target year/class has published fee structure and class-wise amounts configured.</summary>
    [HttpGet("promote-readiness")]
    [Authorize(Policy = MenuPolicies.Students.Edit)]
    [ProducesResponseType(typeof(PromoteReadinessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PromoteReadinessResponse>> GetPromoteReadiness(
        [FromQuery] Guid targetAcademicYearId,
        [FromQuery] Guid targetClassId,
        CancellationToken cancellationToken)
    {
        string? error = await studentRepository
            .GetPromoteTargetValidationErrorAsync(targetAcademicYearId, targetClassId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new PromoteReadinessResponse
        {
            Ready = error is null,
            Message = error
        });
    }

    /// <summary>Promote students from one academic year enrollment to the next.</summary>
    [HttpPost("promote")]
    [Authorize(Policy = MenuPolicies.Students.Edit)]
    [ProducesResponseType(typeof(PromoteStudentsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromoteStudentsResponse>> PromoteStudents(
        [FromBody] PromoteStudentsRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Students is null || request.Students.Count == 0)
        {
            return BadRequest("At least one student is required.");
        }

        var entries = request.Students
            .Select(s => new PromoteStudentEntry(
                s.StudentId,
                s.TargetClassId,
                s.RollNumber,
                s.AdmissionDate))
            .ToList();

        PromoteStudentsResult result = await studentRepository.PromoteStudentsAsync(
            request.SourceAcademicYearId,
            request.TargetAcademicYearId,
            entries,
            cancellationToken).ConfigureAwait(false);

        return Ok(new PromoteStudentsResponse(result.PromotedCount, result.Errors));
    }
}
