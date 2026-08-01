namespace SmartOps.Domain.Modules.Student;

public sealed record UpdateRollNumberEntry(Guid StudentId, string? RollNumber);

public sealed record UpdateRollNumbersResult(int UpdatedCount, IReadOnlyList<string> Errors);
