using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Exam;

public class ExamGradeScaleEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

public class ExamGradeScaleDetailEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid GradeScaleId { get; set; }
    public string Grade { get; set; } = string.Empty;
    public decimal MinPercent { get; set; }
    public decimal MaxPercent { get; set; }
    public decimal? GradePoint { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class ExamGroupEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? GradeScaleId { get; set; }
    public ExamEvaluationType EvaluationType { get; set; }
}

public class ExamEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamGroupId { get; set; }
    public Guid BranchId { get; set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExamType { get; set; } = string.Empty;
    public Guid? AcademicPeriodId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MinPassPercent { get; set; }
    public Guid? GradeScaleId { get; set; }
    public ExamStatus Status { get; set; }
    public bool ResultDeclared { get; set; }
    public DateTime? ResultDeclaredOn { get; set; }
    public Guid? ResultDeclaredBy { get; set; }
    public string? Description { get; set; }
}

public class ExamClassEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ClassId { get; set; }
}

public class ExamMarkComponentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MaxMarks { get; set; }
    public decimal? PassingMarks { get; set; }
    public int DisplayOrder { get; set; }
}

public class ExamScheduleEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public DateOnly ExamDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? RoomNo { get; set; }
    public Guid? InvigilatorId { get; set; }
}

public class ExamStudentMarkEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamScheduleId { get; set; }
    public Guid ComponentId { get; set; }
    public Guid StudentId { get; set; }
    public decimal? MarksObtained { get; set; }
    public bool IsAbsent { get; set; }
    public bool IsExempted { get; set; }
    public string? Remark { get; set; }
}

public class ExamResultEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal MaxMarks { get; set; }
    public decimal Percentage { get; set; }
    public string? Grade { get; set; }
    public int Rank { get; set; }
    public ExamResultStatus Result { get; set; }
    public string? SubjectResults { get; set; }
    public DateTime? DeclaredOn { get; set; }
    public Guid? DeclaredBy { get; set; }
}

public class ExamHallTicketEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string? SeatNo { get; set; }
}
