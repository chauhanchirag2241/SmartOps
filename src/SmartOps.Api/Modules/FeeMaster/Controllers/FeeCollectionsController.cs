using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (master.PublishedOn.HasValue && master.PublishedOn.Value > today)
        {
            return BadRequest("This fee is not published yet.");
        }

        var detail = await feeStudentAmountRepository
            .GetStudentDetailAsync(request.FeeMasterId, studentId, ct)
            .ConfigureAwait(false);
        if (detail is null)
        {
            return NotFound("Student fee detail not found.");
        }

        var paidByHead = await feePaymentRepository
            .GetPaidByHeadAsync(studentId, request.FeeMasterId, ct)
            .ConfigureAwait(false);

        var headMap = detail.Heads
            .Where(h => !h.IsExcluded)
            .ToDictionary(h => h.FeeHeadId);

        var paymentLines = new List<FeePaymentLineEntity>();
        foreach (var line in lines)
        {
            if (!headMap.TryGetValue(line.FeeHeadId, out var head))
            {
                return BadRequest("Invalid fee head.");
            }

            var due = head.Amount ?? head.DefaultAmount ?? 0m;
            paidByHead.TryGetValue(line.FeeHeadId, out var alreadyPaid);
            var balance = Math.Max(0, due - alreadyPaid);
            if (balance <= 0)
            {
                return BadRequest($"'{head.FeeHeadName}' is already fully paid.");
            }

            var payAmount = line.Amount;
            if (!head.IsEditable)
            {
                // Non-editable heads must collect remaining balance as-is.
                payAmount = balance;
            }
            else if (payAmount > balance)
            {
                return BadRequest($"Amount for '{head.FeeHeadName}' exceeds pending balance.");
            }

            paymentLines.Add(new FeePaymentLineEntity
            {
                FeeHeadId = head.FeeHeadId,
                FeeHeadName = head.FeeHeadName,
                DueAmount = due,
                PaidAmount = payAmount,
                IsMandatory = head.IsMandatory,
                IsEditable = head.IsEditable,
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
            PaymentDate = DateTimeOffset.UtcNow,
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
