namespace SmartOps.Domain.Modules.FeeMaster;

public sealed class FeeHeadListModel
{
    public Guid Id { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
    public decimal? Amount { get; set; }
    public string? ApplicableMonths { get; set; }
    public bool IsActive { get; set; }
}

public sealed class FeeHeadPeriodAmountModel
{
    public Guid Id { get; set; }
    public Guid ClassGroupId { get; set; }
    public string? ClassGroupName { get; set; }
    public Guid AcademicPeriodId { get; set; }
    public string? AcademicPeriodName { get; set; }
    public decimal Amount { get; set; }
}

public sealed class FeeHeadDetailModel
{
    public Guid Id { get; set; }
    public Guid FeeMasterId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
    public decimal? Amount { get; set; }
    public string? ApplicableMonths { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<FeeHeadPeriodAmountModel> PeriodAmounts { get; set; } = [];
}
