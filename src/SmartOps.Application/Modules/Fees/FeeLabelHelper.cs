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

    public static string CollectionTypeLabel(FeeCollectionType t) => t switch
    {
        FeeCollectionType.SemesterWise => "Semester wise",
        FeeCollectionType.OneTime => "One time",
        _ => t.ToString()
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
