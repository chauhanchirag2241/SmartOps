namespace SmartOps.Domain.Modules.FeeMaster;

public sealed class FeeMasterDetailModel
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string FeeName { get; set; } = string.Empty;
    public string FeeType { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public DateOnly? DefaultDueDate { get; set; }
    public string ApplicableTo { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<Guid> ClassGroupIds { get; set; } = [];
}
