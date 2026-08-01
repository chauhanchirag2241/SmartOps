namespace SmartOps.Domain.Common.Models;

public sealed class DropdownDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional parent / group id (e.g. class → class group).</summary>
    public Guid? ClassGroupId { get; set; }
}
