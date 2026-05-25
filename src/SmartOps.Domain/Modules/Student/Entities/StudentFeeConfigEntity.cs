using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentFeeConfigEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public bool? IsPercentage { get; set; }
    public string? DiscountRemarks { get; set; }
}
