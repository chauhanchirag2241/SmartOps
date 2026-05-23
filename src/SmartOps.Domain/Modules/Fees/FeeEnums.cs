namespace SmartOps.Domain.Modules.Fees;

public enum FeeCategory
{
    Academic = 0,
    Development = 1,
    Transport = 2,
    Other = 3
}

public enum FeeFrequency
{
    Annual = 0,
    SemiAnnual = 1,
    Quarterly = 2,
    Monthly = 3,
    OneTime = 4
}

public enum FeePaymentCycle
{
    Annual = 0,
    SemiAnnual = 1,
    Quarterly = 2,
    Monthly = 3
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
