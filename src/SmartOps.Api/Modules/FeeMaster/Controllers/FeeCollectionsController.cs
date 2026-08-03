using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.FeeMaster;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Api.Modules.FeeMaster.Controllers;

[ApiController]
[Route("api/fees/collection")]
[Authorize]
public sealed class FeeCollectionsController(
    IFeePaymentRepository feePaymentRepository,
    IFeeMasterRepository feeMasterRepository,
    IFeeStudentAmountRepository feeStudentAmountRepository,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("students/{studentId:guid}")]
    [Authorize(Policy = MenuPolicies.FeeCollection.View)]
    [ProducesResponseType(typeof(FeeCollectionDetailModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeeCollectionDetailModel>> GetStudentDetail(
        Guid studentId,
        CancellationToken ct)
    {
        var detail = await feePaymentRepository
            .GetStudentCollectionDetailAsync(studentId, ct)
            .ConfigureAwait(false);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("students/summaries")]
    [Authorize(Policy = MenuPolicies.FeeCollection.View)]
    [ProducesResponseType(typeof(IReadOnlyList<FeeCollectionStudentSummaryModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeeCollectionStudentSummaryModel>>> GetStudentSummaries(
        [FromBody] Guid[] studentIds,
        CancellationToken ct)
    {
        var summaries = await feePaymentRepository
            .GetStudentCollectionSummariesAsync(studentIds ?? [], ct)
            .ConfigureAwait(false);
        return Ok(summaries);
    }

    [HttpPost("students/{studentId:guid}/collect")]
    [Authorize(Policy = MenuPolicies.FeeCollection.Edit)]
    [ProducesResponseType(typeof(CollectFeeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CollectFeeResponse>> Collect(
        Guid studentId,
        [FromBody] CollectFeeDto request,
        CancellationToken ct)
    {
        if (request is null || request.FeeMasterId == Guid.Empty)
        {
            return BadRequest("Fee master is required.");
        }

        var lines = (request.Lines ?? []).Where(l => l.FeeHeadId != Guid.Empty && l.Amount > 0).ToList();
        if (lines.Count == 0)
        {
            return BadRequest("Select at least one fee head with amount greater than zero.");
        }

        var master = await feeMasterRepository.GetByIdAsync(request.FeeMasterId, ct).ConfigureAwait(false);
        if (master is null)
        {
            return NotFound("Fee master not found.");
        }

        var today = SchoolLocalTime.Today(null);
        if (master.PublishedOn.HasValue && master.PublishedOn.Value > today)
        {
            return BadRequest("This fee is not published yet.");
        }

        var isPeriodWise = string.Equals(
            master.FeeType?.Replace(" ", string.Empty),
            "PeriodWise",
            StringComparison.OrdinalIgnoreCase);

        if (isPeriodWise && (!request.AcademicPeriodId.HasValue || request.AcademicPeriodId == Guid.Empty))
        {
            return BadRequest("Academic period is required for period-wise fee collection.");
        }

        var paidByHead = await feePaymentRepository
            .GetPaidByHeadAsync(studentId, request.FeeMasterId, request.AcademicPeriodId, ct)
            .ConfigureAwait(false);

        Dictionary<Guid, (string Name, bool Mandatory, bool Editable, decimal Due)> headMap;

        if (isPeriodWise)
        {
            var periodDues = await feePaymentRepository
                .GetPeriodHeadDuesAsync(request.FeeMasterId, studentId, request.AcademicPeriodId!.Value, ct)
                .ConfigureAwait(false);
            headMap = periodDues
                .Where(h => !h.IsExcluded)
                .ToDictionary(
                    h => h.FeeHeadId,
                    h => (h.FeeHeadName, h.IsMandatory, h.IsEditable, h.DueAmount));
        }
        else
        {
            var detail = await feeStudentAmountRepository
                .GetStudentDetailAsync(request.FeeMasterId, studentId, ct)
                .ConfigureAwait(false);
            if (detail is null)
            {
                return NotFound("Student fee detail not found.");
            }

            headMap = detail.Heads
                .Where(h => !h.IsExcluded)
                .ToDictionary(
                    h => h.FeeHeadId,
                    h => (h.FeeHeadName, h.IsMandatory, h.IsEditable, h.Amount ?? h.DefaultAmount ?? 0m));
        }

        var paymentLines = new List<FeePaymentLineEntity>();
        foreach (var line in lines)
        {
            if (!headMap.TryGetValue(line.FeeHeadId, out var head))
            {
                return BadRequest("Invalid fee head.");
            }

            var due = head.Due;
            paidByHead.TryGetValue(line.FeeHeadId, out var alreadyPaid);
            var balance = Math.Max(0, due - alreadyPaid);
            if (balance <= 0)
            {
                return BadRequest($"'{head.Name}' is already fully paid.");
            }

            // Partial collection allowed for all heads (editable or locked).
            var payAmount = line.Amount;
            if (payAmount <= 0)
            {
                return BadRequest($"Amount for '{head.Name}' must be greater than zero.");
            }

            if (payAmount > balance)
            {
                return BadRequest($"Amount for '{head.Name}' exceeds pending balance ({balance:0.##}).");
            }

            paymentLines.Add(new FeePaymentLineEntity
            {
                FeeHeadId = line.FeeHeadId,
                FeeHeadName = head.Name,
                DueAmount = due,
                PaidAmount = payAmount,
                IsMandatory = head.Mandatory,
                IsEditable = head.Editable,
            });
        }

        var paymentMethod = FeePaymentMethods.Normalize(request.PaymentMethod);
        if (string.IsNullOrEmpty(paymentMethod))
        {
            return BadRequest("Invalid payment method. Use Cash, UPI, Cheque, Card, BankTransfer, or Other.");
        }

        var payment = new FeePaymentEntity
        {
            StudentId = studentId,
            FeeMasterId = request.FeeMasterId,
            AcademicPeriodId = request.AcademicPeriodId,
            BranchId = master.BranchId,
            PaymentDate = SchoolLocalTime.Now(),
            PaymentMethod = paymentMethod,
            TotalAmount = paymentLines.Sum(l => l.PaidAmount),
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
            CollectedByUserId = currentUser.UserId,
        };

        var paymentId = await feePaymentRepository
            .CreatePaymentAsync(payment, paymentLines, ct)
            .ConfigureAwait(false);

        return Ok(new CollectFeeResponse(paymentId, "Fee collected successfully"));
    }
}
