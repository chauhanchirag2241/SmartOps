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

    public static string StatusForHead(decimal total, decimal paid)
    {
        decimal due = Math.Max(0, total - paid);
        if (total <= 0)
        {
            return "No fees";
        }

        if (due <= 0)
        {
            return "Paid";
        }

        return paid > 0 ? "Partial" : "Unpaid";
    }
}
