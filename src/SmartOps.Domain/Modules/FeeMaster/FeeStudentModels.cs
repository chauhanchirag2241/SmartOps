namespace SmartOps.Domain.Modules.FeeMaster;

public sealed class FeeStudentListModel
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? RollNumber { get; set; }
    public string? AdmissionNo { get; set; }
    public string? ClassName { get; set; }
    public string? Section { get; set; }
    public Guid? ClassId { get; set; }
    public decimal? AmountSummary { get; set; }
    public bool CanEdit { get; set; }
    public bool CanRemove { get; set; }
    public bool HasOverrides { get; set; }
}

public sealed class FeeStudentHeadAmountModel
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
    public Guid? AcademicPeriodId { get; set; }
    public string? PeriodLabel { get; set; }
    public int? PeriodIndex { get; set; }
    public decimal? DefaultAmount { get; set; }
    public decimal? Amount { get; set; }
    public bool IsExcluded { get; set; }
    public bool HasOverride { get; set; }
}

public sealed class FeeStudentPeriodGroupModel
{
    public Guid AcademicPeriodId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public int PeriodIndex { get; set; }
    public IReadOnlyList<FeeStudentHeadAmountModel> Heads { get; set; } = [];
}

public sealed class FeeStudentDetailModel
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public bool IsPeriodWise { get; set; }
    /// <summary>Flat heads (non period-wise) or flattened head×period rows.</summary>
    public IReadOnlyList<FeeStudentHeadAmountModel> Heads { get; set; } = [];
    /// <summary>Period groups for UI when <see cref="IsPeriodWise"/>.</summary>
    public IReadOnlyList<FeeStudentPeriodGroupModel> Periods { get; set; } = [];
}
