using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public static class FeeCategoryHelper
{
    public static bool IsDiscount(FeeCategory category) => category == FeeCategory.Discount;

    public static bool IsDiscount(int category) => category == (int)FeeCategory.Discount;

    public static decimal SignedAnnualTotal(FeeCategory category, decimal annualTotal)
    {
        if (annualTotal == 0)
        {
            return 0;
        }

        return IsDiscount(category) ? -Math.Abs(annualTotal) : annualTotal;
    }

    public static decimal SignedInstallmentAmount(FeeCategory category, decimal amount)
    {
        if (amount == 0)
        {
            return 0;
        }

        return IsDiscount(category) ? -Math.Abs(amount) : amount;
    }
}
