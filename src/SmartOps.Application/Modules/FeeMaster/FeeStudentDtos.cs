namespace SmartOps.Application.Modules.FeeMaster;

public sealed class FeeStudentHeadAmountDto
{
    public Guid FeeHeadId { get; set; }
    public Guid? AcademicPeriodId { get; set; }
    public decimal? Amount { get; set; }
    public bool? IsExcluded { get; set; }
}

public sealed class AddFeeStudentDto
{
    public Guid StudentId { get; set; }
    public IReadOnlyList<FeeStudentHeadAmountDto> Amounts { get; set; } = [];
}

public sealed class UpdateFeeStudentDto
{
    public IReadOnlyList<FeeStudentHeadAmountDto> Amounts { get; set; } = [];
}

public sealed record AddFeeStudentResponse(string Message, Guid StudentId);
