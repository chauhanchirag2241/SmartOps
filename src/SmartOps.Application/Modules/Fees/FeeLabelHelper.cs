using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public static class FeeLabelHelper
{
    public static string CategoryLabel(FeeCategory c) => c switch
    {
        FeeCategory.Academic => "Academic",
        FeeCategory.Development => "Development",
        FeeCategory.Transport => "Transport",
        _ => "Other"
    };

    public static string FrequencyLabel(FeeFrequency f) => f switch
    {
        FeeFrequency.Annual => "Annual",
        FeeFrequency.SemiAnnual => "Semi-annual",
        FeeFrequency.Quarterly => "Quarterly",
        FeeFrequency.Monthly => "Monthly",
        FeeFrequency.OneTime => "One-time",
        _ => f.ToString()
    };

    public static string PaymentCycleLabel(FeePaymentCycle c) => c switch
    {
        FeePaymentCycle.Annual => "Annual",
        FeePaymentCycle.SemiAnnual => "Semi-annual",
        FeePaymentCycle.Quarterly => "Quarterly",
        FeePaymentCycle.Monthly => "Monthly",
        _ => c.ToString()
    };

    public static string VersionStatusLabel(FeeStructureVersionStatus status) => status switch
    {
        FeeStructureVersionStatus.Draft => "Draft",
        FeeStructureVersionStatus.Published => "Published",
        FeeStructureVersionStatus.Active => "Active",
        FeeStructureVersionStatus.Archived => "Archived",
        _ => status.ToString()
    };

    public static string PaymentModeLabel(FeePaymentMode m) => m switch
    {
        FeePaymentMode.Cash => "Cash",
        FeePaymentMode.Upi => "UPI",
        FeePaymentMode.BankTransfer => "Bank transfer / NEFT",
        FeePaymentMode.Cheque => "Cheque",
        FeePaymentMode.Card => "Card (POS)",
        _ => m.ToString()
    };

    public static string PaymentStatus(decimal total, decimal paid)
    {
        if (total <= 0)
        {
            return "No fees";
        }

        if (paid <= 0)
        {
            return "Not paid";
        }

        return paid >= total ? "Fully paid" : "Partial";
    }
}
