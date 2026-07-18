namespace SmartOps.Domain.Modules.Fees;

public enum FeeCategory
{
    Academic = 0,
    Development = 1,
    Transport = 2,
    Other = 3,
    Discount = 4
}

/// <summary>How a fee head is collected: by configured academic period or once per academic year.</summary>
public enum FeeCollectionType
{
    PeriodWise = 0,
    OneTime = 1
}

public enum FeePaymentMode
{
    Cash = 0,
    Upi = 1,
    BankTransfer = 2,
    Cheque = 3,
    Card = 4
}

public enum FeeStructureVersionStatus
{
    Draft = 0,
    Published = 1,
    Active = 2,
    Archived = 3
}
