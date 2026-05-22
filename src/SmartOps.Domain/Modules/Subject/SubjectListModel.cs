namespace SmartOps.Domain.Modules.Subject;

public sealed class SubjectListModel
{
    public Guid Id { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string? SubjectType { get; set; }
    public string? SubjectCategory { get; set; }
    public string? Medium { get; set; }
    public bool IsActive { get; set; }
}
