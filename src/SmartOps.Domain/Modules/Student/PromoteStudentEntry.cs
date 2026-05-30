namespace SmartOps.Domain.Modules.Student;

public sealed record PromoteStudentEntry(
    Guid StudentId,
    Guid TargetClassId,
    string? RollNumber,
    DateOnly? AdmissionDate);

public sealed record PromoteStudentsResult(int PromotedCount, IReadOnlyList<string> Errors);
