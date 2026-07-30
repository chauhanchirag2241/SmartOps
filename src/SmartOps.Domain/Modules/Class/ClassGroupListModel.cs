namespace SmartOps.Domain.Modules.Class;

/// <summary>
/// Flat projection for class-group list (Config + SmartOps).
/// </summary>
public class ClassGroupListModel
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string? Description { get; set; }
    public int SectionCount { get; set; }
    public int SubjectCount { get; set; }
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}

/// <summary>
/// Subject assigned to a class group.
/// </summary>
public class ClassGroupSubjectListModel
{
    public Guid Id { get; set; }
    public Guid ClassGroupId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public string SubjectCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}
