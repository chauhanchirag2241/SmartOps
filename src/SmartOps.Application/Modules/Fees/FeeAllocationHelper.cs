namespace SmartOps.Application.Modules.Fees;

/// <summary>
/// Distributes a payment pool across fee heads in order (FIFO / waterfall).
/// </summary>
public static class FeeAllocationHelper
{
    public sealed record HeadAmount(Guid FeeTypeId, decimal Amount);

    public sealed record HeadAllocation(Guid FeeTypeId, decimal TotalAmount, decimal PaidAmount, decimal DueAmount);

    /// <summary>
    /// Apply <paramref name="totalPaid"/> to heads in list order until the pool is exhausted.
    /// </summary>
    public static IList<HeadAllocation> DistributePaid(IReadOnlyList<HeadAmount> heads, decimal totalPaid)
    {
        decimal remainingPool = Math.Max(0, totalPaid);
        var result = new List<HeadAllocation>(heads.Count);

        foreach (HeadAmount head in heads)
        {
            decimal total = Math.Max(0, head.Amount);
            decimal paid = Math.Min(total, remainingPool);
            remainingPool -= paid;
            decimal due = Math.Max(0, total - paid);
            result.Add(new HeadAllocation(head.FeeTypeId, total, paid, due));
        }

        return result;
    }

    /// <summary>
    /// Allocate a new payment to selected heads using the same waterfall due amounts shown in the UI.
    /// </summary>
    public static IList<(Guid FeeTypeId, decimal Amount)> AllocateToSelectedHeads(
        IEnumerable<HeadAllocation> distributed,
        decimal paymentAmount,
        IReadOnlySet<Guid> selectedFeeTypeIds)
    {
        decimal remaining = Math.Max(0, paymentAmount);
        var allocations = new List<(Guid FeeTypeId, decimal Amount)>();

        foreach (HeadAllocation head in distributed)
        {
            if (!selectedFeeTypeIds.Contains(head.FeeTypeId) || head.DueAmount <= 0)
            {
                continue;
            }

            decimal alloc = Math.Min(head.DueAmount, remaining);
            if (alloc <= 0)
            {
                break;
            }

            allocations.Add((head.FeeTypeId, alloc));
            remaining -= alloc;
        }

        return allocations;
    }

    public static decimal SumDueOnSelected(IEnumerable<HeadAllocation> distributed, IReadOnlySet<Guid> selectedFeeTypeIds) =>
        distributed.Where(h => selectedFeeTypeIds.Contains(h.FeeTypeId)).Sum(h => h.DueAmount);

    public sealed record InstallmentDue(Guid InstallmentId, Guid FeeTypeId, decimal DueAmount);

    /// <summary>
    /// Allocate payment across explicitly selected installments (exact selection).
    /// </summary>
    public static IList<(Guid FeeTypeId, Guid InstallmentId, decimal Amount)> AllocateToSelectedInstallments(
        IEnumerable<InstallmentDue> installments,
        decimal paymentAmount,
        IReadOnlySet<Guid> selectedInstallmentIds)
    {
        decimal remaining = Math.Max(0, paymentAmount);
        var allocations = new List<(Guid FeeTypeId, Guid InstallmentId, decimal Amount)>();

        foreach (InstallmentDue inst in installments.OrderBy(i => i.InstallmentId))
        {
            if (!selectedInstallmentIds.Contains(inst.InstallmentId) || inst.DueAmount <= 0)
            {
                continue;
            }

            decimal alloc = Math.Min(inst.DueAmount, remaining);
            if (alloc <= 0)
            {
                break;
            }

            allocations.Add((inst.FeeTypeId, inst.InstallmentId, alloc));
            remaining -= alloc;
        }

        return allocations;
    }

    public static decimal SumDueOnSelectedInstallments(
        IEnumerable<InstallmentDue> installments,
        IReadOnlySet<Guid> selectedInstallmentIds) =>
        installments.Where(i => selectedInstallmentIds.Contains(i.InstallmentId)).Sum(i => i.DueAmount);

    public static string StatusForHead(decimal total, decimal paid) =>
        StatusForPeriod(total, paid, periodEnd: null);

    /// <summary>
    /// Paid when fully paid; Partial when some payment; Pending when none;
    /// Overdue when period end has passed and payment is incomplete.
    /// </summary>
    public static string StatusForPeriod(decimal total, decimal paid, DateOnly? periodEnd)
    {
        if (total <= 0)
        {
            return "No fees";
        }

        decimal due = Math.Max(0, total - paid);
        if (due <= 0)
        {
            return "Paid";
        }

        if (paid > 0)
        {
            return "Partial";
        }

        if (periodEnd.HasValue && periodEnd.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return "Overdue";
        }

        return "Pending";
    }
}
