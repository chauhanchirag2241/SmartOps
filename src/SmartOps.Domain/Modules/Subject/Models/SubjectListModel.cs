namespace SmartOps.Domain.Modules.Subject.Models;

public sealed class SubjectListModel
{
    public Guid Id { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectCategory { get; set; } = string.Empty;
    public string Medium { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
