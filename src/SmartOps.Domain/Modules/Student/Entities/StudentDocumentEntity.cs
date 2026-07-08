using System;
using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentDocumentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string DocumentName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
}
