using System.Text.Json.Serialization;
using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentCustomFieldEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }

    [JsonPropertyName("label")]
    public string FieldLabel { get; set; } = null!;

    [JsonPropertyName("value")]
    public string? FieldValue { get; set; }
}
