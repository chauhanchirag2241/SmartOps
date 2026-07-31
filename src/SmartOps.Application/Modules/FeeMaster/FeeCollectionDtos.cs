namespace SmartOps.Application.Modules.FeeMaster;

public sealed class CollectFeeLineDto
{
    public Guid FeeHeadId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class CollectFeeDto
{
    public Guid FeeMasterId { get; set; }
    public Guid? AcademicPeriodId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Remarks { get; set; }
    public IReadOnlyList<CollectFeeLineDto> Lines { get; set; } = [];
}

public static class FeePaymentMethods
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cash",
        "UPI",
        "Cheque",
        "Card",
        "BankTransfer",
        "Other",
    };

    public static string Normalize(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Cash";
        }

        foreach (var allowed in Allowed)
        {
            if (string.Equals(allowed, trimmed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(allowed.Replace(" ", string.Empty), trimmed.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }

        return string.Empty;
    }
}

public sealed record CollectFeeResponse(Guid PaymentId, string Message);
