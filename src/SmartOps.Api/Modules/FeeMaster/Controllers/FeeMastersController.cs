using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.FeeMaster;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.AcademicPeriod;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Api.Modules.FeeMaster.Controllers;

[ApiController]
[Route("api/fees/master")]
[Authorize]
public sealed class FeeMastersController(
    IFeeMasterRepository feeMasterRepository,
    IFeeHeadRepository feeHeadRepository,
    IFeeStudentAmountRepository feeStudentAmountRepository,
    IFeePaymentRepository feePaymentRepository,
    IAcademicPeriodRepository academicPeriodRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.FeeMaster.Add)]
    [ProducesResponseType(typeof(CreateFeeMasterResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateFeeMasterResponse>> Create(
        [FromBody] CreateFeeMasterDto request,
        CancellationToken ct)
    {
        var validationError = ValidateFeeMasterRequest(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var dateError = ValidateFeeDates(request.PublishedOn, request.DefaultDueDate, null, null);
        if (dateError is not null)
        {
            return BadRequest(dateError);
        }

        var entity = request.ToEntity();
        var id = await feeMasterRepository.CreateAsync(entity, ct).ConfigureAwait(false);

        if (string.Equals(entity.ApplicableTo, "ClassWise", StringComparison.OrdinalIgnoreCase))
        {
            await feeMasterRepository
                .SaveClassGroupIdsAsync(id, entity.BranchId, request.ClassGroupIds ?? [], allowRemove: true, ct)
                .ConfigureAwait(false);
        }

        return Ok(new CreateFeeMasterResponse("Fee created successfully", id));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    [ProducesResponseType(typeof(PagedResult<FeeMasterListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? filter = "All",
        CancellationToken ct = default)
    {
        var result = await feeMasterRepository
            .GetAllAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    [ProducesResponseType(typeof(FeeMasterDetailModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeeMasterDetailModel>> GetById(Guid id, CancellationToken ct)
    {
        var fee = await feeMasterRepository.GetByIdAsync(id, ct, includeInactive: true).ConfigureAwait(false);
        if (fee is null)
        {
            return NotFound();
        }

        var classGroupIds = await feeMasterRepository.GetClassGroupIdsAsync(id, ct).ConfigureAwait(false);
        return Ok(new FeeMasterDetailModel
        {
            Id = fee.Id,
            BranchId = fee.BranchId,
            FeeName = fee.FeeName,
            FeeType = fee.FeeType,
            PublishedOn = fee.PublishedOn,
            DefaultDueDate = fee.DefaultDueDate,
            ApplicableTo = fee.ApplicableTo,
            Description = fee.Description,
            IsActive = fee.IsActive,
            ClassGroupIds = classGroupIds,
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateFeeMasterDto request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Fee data is required.");
        }

        var validationError = ValidateFeeMasterRequest(request, requireClassGroups: true);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var existing = await feeMasterRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        if (IsPublishStarted(existing.PublishedOn))
        {
            return BadRequest("Fee master cannot be edited after the published-on date has started.");
        }

        var dateError = ValidateFeeDates(
            request.PublishedOn,
            request.DefaultDueDate,
            existing.PublishedOn,
            existing.DefaultDueDate);
        if (dateError is not null)
        {
            return BadRequest(dateError);
        }

        var entity = request.ToEntity();
        entity.Id = id;
        entity.BranchId = existing.BranchId;
        entity.VersionNo = existing.VersionNo;
        entity.CreatedBy = existing.CreatedBy;
        entity.CreatedOn = existing.CreatedOn;

        await feeMasterRepository.UpdateAsync(entity, ct).ConfigureAwait(false);

        if (string.Equals(entity.ApplicableTo, "ClassWise", StringComparison.OrdinalIgnoreCase))
        {
            // Edit: may add class groups, but cannot remove existing ones.
            await feeMasterRepository
                .SaveClassGroupIdsAsync(id, existing.BranchId, request.ClassGroupIds ?? [], allowRemove: false, ct)
                .ConfigureAwait(false);
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/basic")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateBasic(Guid id, [FromBody] UpdateFeeMasterBasicDto request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.FeeName))
        {
            return BadRequest("Fee name is required.");
        }

        var existing = await feeMasterRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        if (IsPublishStarted(existing.PublishedOn))
        {
            return BadRequest("Fee master cannot be edited after the published-on date has started.");
        }

        var dateError = ValidateFeeDates(
            request.PublishedOn,
            request.DefaultDueDate,
            existing.PublishedOn,
            existing.DefaultDueDate);
        if (dateError is not null)
        {
            return BadRequest(dateError);
        }

        existing.FeeName = request.FeeName.Trim();
        existing.PublishedOn = request.PublishedOn;
        existing.DefaultDueDate = request.DefaultDueDate;
        existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await feeMasterRepository.UpdateBasicAsync(existing, ct).ConfigureAwait(false);

        if (string.Equals(existing.ApplicableTo, "ClassWise", StringComparison.OrdinalIgnoreCase)
            && request.ClassGroupIds is not null)
        {
            if (request.ClassGroupIds.Count == 0)
            {
                return BadRequest("Select at least one class for class-wise fees.");
            }

            await feeMasterRepository
                .SaveClassGroupIdsAsync(id, existing.BranchId, request.ClassGroupIds, allowRemove: false, ct)
                .ConfigureAwait(false);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await feeMasterRepository.DeleteAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var history = await auditLogRepository
            .GetEntityHistoryAsync(DatabaseConfig.TableFeeMaster, id, page, pageSize, ct)
            .ConfigureAwait(false);
        return Ok(history);
    }

    // ── Fee heads ──────────────────────────────────────────────────────────

    [HttpGet("{feeMasterId:guid}/heads")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    [ProducesResponseType(typeof(PagedResult<FeeHeadListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeads(
        Guid feeMasterId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? filter = "All",
        CancellationToken ct = default)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct, includeInactive: true).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        var result = await feeHeadRepository
            .GetByFeeMasterAsync(feeMasterId, pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("{feeMasterId:guid}/heads")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Add)]
    [ProducesResponseType(typeof(CreateFeeHeadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateFeeHeadResponse>> CreateHead(
        Guid feeMasterId,
        [FromBody] CreateFeeHeadDto request,
        CancellationToken ct)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        if (IsPublishStarted(parent.PublishedOn))
        {
            return BadRequest("Fee heads cannot be added after the published-on date has started.");
        }

        var validationError = await ValidateFeeHeadRequestAsync(parent, request, ct).ConfigureAwait(false);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var entity = request.ToEntity(feeMasterId);
        NormalizeAmountsForParent(parent, entity, request);
        var periods = ShouldUsePeriodAmounts(parent) ? request.ToPeriodEntities() : [];
        var id = await feeHeadRepository.CreateAsync(entity, periods, ct).ConfigureAwait(false);
        return Ok(new CreateFeeHeadResponse("Fee head created successfully", id));
    }

    [HttpGet("heads/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    [ProducesResponseType(typeof(FeeHeadDetailModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeeHeadDetailModel>> GetHead(Guid id, CancellationToken ct)
    {
        var head = await feeHeadRepository.GetByIdAsync(id, ct, includeInactive: true).ConfigureAwait(false);
        return head is null ? NotFound() : Ok(head);
    }

    [HttpPut("heads/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateHead(Guid id, [FromBody] CreateFeeHeadDto request, CancellationToken ct)
    {
        var existing = await feeHeadRepository.GetEntityByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        var parent = await feeMasterRepository.GetByIdAsync(existing.FeeMasterId, ct).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        if (IsPublishStarted(parent.PublishedOn))
        {
            return BadRequest("Fee heads cannot be edited after the published-on date has started.");
        }

        var validationError = await ValidateFeeHeadRequestAsync(parent, request, ct).ConfigureAwait(false);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var entity = request.ToEntity(existing.FeeMasterId);
        NormalizeAmountsForParent(parent, entity, request);
        entity.Id = id;
        entity.BranchId = existing.BranchId;
        entity.VersionNo = existing.VersionNo;
        entity.CreatedBy = existing.CreatedBy;
        entity.CreatedOn = existing.CreatedOn;

        var periods = ShouldUsePeriodAmounts(parent) ? request.ToPeriodEntities() : [];
        await feeHeadRepository.UpdateAsync(entity, periods, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("heads/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteHead(Guid id, CancellationToken ct)
    {
        var existing = await feeHeadRepository.GetEntityByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        var parent = await feeMasterRepository.GetByIdAsync(existing.FeeMasterId, ct).ConfigureAwait(false);
        if (parent is not null && IsPublishStarted(parent.PublishedOn))
        {
            return BadRequest("Fee heads cannot be deleted after the published-on date has started.");
        }

        await feeHeadRepository.DeleteAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("heads/{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    public async Task<IActionResult> GetHeadHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var history = await auditLogRepository
            .GetEntityHistoryAsync(DatabaseConfig.TableFeeHead, id, page, pageSize, ct)
            .ConfigureAwait(false);
        return Ok(history);
    }

    // ── Fee students ───────────────────────────────────────────────────────

    [HttpGet("{feeMasterId:guid}/students")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    [ProducesResponseType(typeof(PagedResult<FeeStudentListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents(
        Guid feeMasterId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid[]? classIds = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken ct = default)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct, includeInactive: true).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        var filterClassIds = (classIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (classId.HasValue && classId.Value != Guid.Empty && !filterClassIds.Contains(classId.Value))
        {
            filterClassIds.Add(classId.Value);
        }

        var result = await feeStudentAmountRepository
            .GetStudentsAsync(
                feeMasterId,
                parent.ApplicableTo,
                pageIndex,
                pageSize,
                searchTerm,
                filterClassIds,
                sortColumn,
                sortDirection,
                ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{feeMasterId:guid}/students/{studentId:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.View)]
    [ProducesResponseType(typeof(FeeStudentDetailModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeeStudentDetailModel>> GetStudent(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken ct)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct, includeInactive: true).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        var detail = await feeStudentAmountRepository.GetStudentDetailAsync(feeMasterId, studentId, ct).ConfigureAwait(false);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{feeMasterId:guid}/students")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Add)]
    [ProducesResponseType(typeof(AddFeeStudentResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AddFeeStudentResponse>> AddStudent(
        Guid feeMasterId,
        [FromBody] AddFeeStudentDto request,
        CancellationToken ct)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        if (!string.Equals(parent.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Students can only be added for Student-wise fee masters.");
        }

        if (request.StudentId == Guid.Empty)
        {
            return BadRequest("Student is required.");
        }

        if (await feeStudentAmountRepository.StudentExistsOnMasterAsync(feeMasterId, request.StudentId, ct).ConfigureAwait(false))
        {
            return BadRequest("Student is already added to this fee.");
        }

        var headsPage = await feeHeadRepository
            .GetByFeeMasterAsync(feeMasterId, 1, 500, null, null, null, "Active", ct)
            .ConfigureAwait(false);
        var heads = headsPage.Items.ToList();
        if (heads.Count == 0)
        {
            return BadRequest("Add fee heads before assigning students.");
        }

        var amountByHead = (request.Amounts ?? [])
            .GroupBy(a => a.FeeHeadId)
            .ToDictionary(g => g.Key, g => g.Last());

        var rows = new List<FeeStudentAmountEntity>();
        foreach (var head in heads)
        {
            amountByHead.TryGetValue(head.Id, out var dto);
            var amount = dto?.Amount ?? head.Amount;
            rows.Add(new FeeStudentAmountEntity
            {
                FeeHeadId = head.Id,
                Amount = amount,
                IsExcluded = false,
            });
        }

        await feeStudentAmountRepository
            .UpsertOverridesAsync(feeMasterId, request.StudentId, parent.BranchId, rows, ct)
            .ConfigureAwait(false);

        return Ok(new AddFeeStudentResponse("Student added successfully", request.StudentId));
    }

    [HttpPut("{feeMasterId:guid}/students/{studentId:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateStudent(
        Guid feeMasterId,
        Guid studentId,
        [FromBody] UpdateFeeStudentDto request,
        CancellationToken ct)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        var detail = await feeStudentAmountRepository.GetStudentDetailAsync(feeMasterId, studentId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return NotFound();
        }

        if (await feePaymentRepository.HasPaymentAsync(studentId, feeMasterId, ct).ConfigureAwait(false))
        {
            return BadRequest("Student fee amounts cannot be edited after fee has been collected.");
        }

        var headMap = detail.Heads.ToDictionary(
            h => (h.FeeHeadId, PeriodId: h.AcademicPeriodId ?? Guid.Empty));
        var rows = new List<FeeStudentAmountEntity>();

        foreach (var item in request.Amounts ?? [])
        {
            var periodKey = item.AcademicPeriodId ?? Guid.Empty;
            if (!headMap.TryGetValue((item.FeeHeadId, periodKey), out var head))
            {
                return BadRequest("Invalid fee head.");
            }

            var exclude = item.IsExcluded == true;
            if (exclude && head.IsMandatory)
            {
                return BadRequest($"Cannot exclude mandatory fee head '{head.FeeHeadName}'.");
            }

            var excludeChanged = item.IsExcluded.HasValue && item.IsExcluded.Value != head.IsExcluded;
            var effectiveDefault = head.DefaultAmount;
            var currentEffective = head.IsExcluded ? (decimal?)null : (head.HasOverride ? head.Amount : head.DefaultAmount);
            var isStudentWise = string.Equals(parent.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase);
            var canEditAmount = head.IsEditable || isStudentWise || head.HasOverride;

            if (canEditAmount && item.Amount.HasValue)
            {
                var newAmount = item.Amount;
                var differsFromDefault = newAmount != effectiveDefault || head.IsExcluded;
                var differsFromCurrent = newAmount != currentEffective || exclude;
                if (differsFromDefault || differsFromCurrent || excludeChanged)
                {
                    rows.Add(new FeeStudentAmountEntity
                    {
                        FeeHeadId = item.FeeHeadId,
                        AcademicPeriodId = item.AcademicPeriodId,
                        Amount = exclude ? null : newAmount,
                        IsExcluded = exclude,
                    });
                }
            }
            else if (excludeChanged && !head.IsMandatory)
            {
                rows.Add(new FeeStudentAmountEntity
                {
                    FeeHeadId = item.FeeHeadId,
                    AcademicPeriodId = item.AcademicPeriodId,
                    Amount = exclude ? null : (head.HasOverride ? head.Amount : head.DefaultAmount),
                    IsExcluded = exclude,
                });
            }
        }

        if (rows.Count > 0)
        {
            await feeStudentAmountRepository
                .UpsertOverridesAsync(feeMasterId, studentId, parent.BranchId, rows, ct)
                .ConfigureAwait(false);
        }

        return NoContent();
    }

    [HttpDelete("{feeMasterId:guid}/students/{studentId:guid}")]
    [Authorize(Policy = MenuPolicies.FeeMaster.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveStudent(Guid feeMasterId, Guid studentId, CancellationToken ct)
    {
        var parent = await feeMasterRepository.GetByIdAsync(feeMasterId, ct).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound();
        }

        var isStudentWise = string.Equals(parent.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase);
        if (isStudentWise)
        {
            await feeStudentAmountRepository.SoftDeleteByStudentAsync(feeMasterId, studentId, ct).ConfigureAwait(false);
            return NoContent();
        }

        var detail = await feeStudentAmountRepository.GetStudentDetailAsync(feeMasterId, studentId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return NotFound();
        }

        var optionalHeads = detail.Heads.Where(h => !h.IsMandatory).ToList();
        if (optionalHeads.Count == 0)
        {
            return BadRequest("All fee heads are mandatory; this student cannot be removed.");
        }

        var rows = optionalHeads.Select(h => new FeeStudentAmountEntity
        {
            FeeHeadId = h.FeeHeadId,
            Amount = null,
            IsExcluded = true,
        }).ToList();

        await feeStudentAmountRepository
            .UpsertOverridesAsync(feeMasterId, studentId, parent.BranchId, rows, ct)
            .ConfigureAwait(false);

        return NoContent();
    }

    private static string? ValidateFeeMasterRequest(CreateFeeMasterDto request, bool requireClassGroups = true)
    {
        if (string.IsNullOrWhiteSpace(request.FeeName))
        {
            return "Fee name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.FeeType)
            || !FeeMasterMappingExtensions.AllowedFeeTypes.Contains(request.FeeType.Trim()))
        {
            return "Fee type must be One Time, Monthly, or Period Wise.";
        }

        if (string.IsNullOrWhiteSpace(request.ApplicableTo)
            || !FeeMasterMappingExtensions.AllowedApplicableTo.Contains(request.ApplicableTo.Trim()))
        {
            return "Applicable to must be Class wise or Student wise.";
        }

        var isClassWise = string.Equals(
            FeeMasterMappingExtensions.ToEntity(request).ApplicableTo,
            "ClassWise",
            StringComparison.OrdinalIgnoreCase);
        if (requireClassGroups && isClassWise && (request.ClassGroupIds is null || request.ClassGroupIds.Count == 0))
        {
            return "Select at least one class for class-wise fees.";
        }

        return null;
    }

    private static bool IsPublishStarted(DateOnly? publishedOn)
    {
        if (!publishedOn.HasValue)
        {
            return false;
        }

        return publishedOn.Value <= SchoolLocalTime.Today(null);
    }

    private static string? ValidateFeeDates(
        DateOnly? publishedOn,
        DateOnly? defaultDueDate,
        DateOnly? existingPublishedOn,
        DateOnly? existingDefaultDueDate)
    {
        var today = SchoolLocalTime.Today(null);

        if (publishedOn.HasValue
            && publishedOn.Value < today
            && publishedOn != existingPublishedOn)
        {
            return "Published on cannot be a past date.";
        }

        if (defaultDueDate.HasValue
            && defaultDueDate.Value < today
            && defaultDueDate != existingDefaultDueDate)
        {
            return "Default due date cannot be a past date.";
        }

        if (publishedOn.HasValue
            && defaultDueDate.HasValue
            && defaultDueDate.Value < publishedOn.Value)
        {
            return "Default due date must be on or after published on.";
        }

        return null;
    }

    private async Task<string?> ValidateFeeHeadRequestAsync(
        FeeMasterEntity parent,
        CreateFeeHeadDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FeeHeadName))
        {
            return "Fee head name is required.";
        }

        var isStudentWise = string.Equals(parent.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase);
        var feeType = parent.FeeType;

        if (isStudentWise)
        {
            if (request.Amount is null)
            {
                return "Amount is required.";
            }

            return null;
        }

        if (string.Equals(feeType, "OneTime", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Amount is null)
            {
                return "Default amount is required.";
            }

            return null;
        }

        if (string.Equals(feeType, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Amount is null)
            {
                return "Default amount is required.";
            }

            var months = request.ApplicableMonths?.Where(m => m is >= 1 and <= 12).Distinct().ToArray() ?? [];
            if (months.Length == 0)
            {
                return "Select at least one month.";
            }

            return null;
        }

        if (string.Equals(feeType, "PeriodWise", StringComparison.OrdinalIgnoreCase))
        {
            var periods = request.PeriodAmounts ?? [];
            if (periods.Count == 0)
            {
                return "Add default amounts for at least one class period.";
            }

            foreach (var group in periods.GroupBy(p => p.ClassGroupId))
            {
                var setup = await academicPeriodRepository.GetByClassAsync(group.Key, ct).ConfigureAwait(false);
                var validIds = setup.Select(p => p.Id).ToHashSet();
                if (group.Any(p => !validIds.Contains(p.AcademicPeriodId)))
                {
                    return "One or more academic periods do not belong to the selected class.";
                }
            }

            return null;
        }

        return "Unsupported fee type.";
    }

    private static bool ShouldUsePeriodAmounts(FeeMasterEntity parent) =>
        string.Equals(parent.ApplicableTo, "ClassWise", StringComparison.OrdinalIgnoreCase)
        && string.Equals(parent.FeeType, "PeriodWise", StringComparison.OrdinalIgnoreCase);

    private static void NormalizeAmountsForParent(
        FeeMasterEntity parent,
        FeeHeadEntity entity,
        CreateFeeHeadDto request)
    {
        var isStudentWise = string.Equals(parent.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase);
        if (isStudentWise)
        {
            entity.ApplicableMonths = null;
            return;
        }

        if (string.Equals(parent.FeeType, "PeriodWise", StringComparison.OrdinalIgnoreCase))
        {
            entity.Amount = null;
            entity.ApplicableMonths = null;
            return;
        }

        if (string.Equals(parent.FeeType, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            entity.ApplicableMonths = FeeHeadMappingExtensions.SerializeMonths(request.ApplicableMonths);
            return;
        }

        entity.ApplicableMonths = null;
    }
}
