using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Application.Modules.Exam;

// ── Grade scales ─────────────────────────────────────────────

public record ExamGradeScaleDetailDto(
    Guid? Id,
    string Grade,
    decimal MinPercent,
    decimal MaxPercent,
    decimal? GradePoint,
    string? Description,
    int DisplayOrder
);

public record ExamGradeScaleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    IList<ExamGradeScaleDetailDto> Grades
);

public record SaveExamGradeScaleRequestDto(
    string Name,
    string? Description,
    bool IsDefault,
    IList<ExamGradeScaleDetailDto> Grades
);

// ── Exam groups ──────────────────────────────────────────────

public record ExamGroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid AcademicYearId,
    string AcademicYearTitle,
    Guid? GradeScaleId,
    string? GradeScaleName,
    ExamEvaluationType EvaluationType,
    string EvaluationTypeLabel,
    int ExamCount
);

public record SaveExamGroupRequestDto(
    string Name,
    string? Description,
    Guid AcademicYearId,
    Guid? GradeScaleId,
    ExamEvaluationType EvaluationType
);

// ── Exams ────────────────────────────────────────────────────

public record ExamMarkComponentDto(
    Guid? Id,
    string Name,
    decimal MaxMarks,
    decimal? PassingMarks,
    int DisplayOrder
);

public record ExamClassInfoDto(
    Guid ClassId,
    string ClassName
);

public record ExamListItemDto(
    Guid Id,
    string Name,
    string ExamType,
    Guid ExamGroupId,
    string ExamGroupName,
    DateOnly StartDate,
    DateOnly EndDate,
    ExamStatus Status,
    string StatusLabel,
    bool ResultDeclared,
    decimal TotalMaxMarks,
    int SubjectCount,
    IList<ExamClassInfoDto> Classes
);

public record ExamDetailDto(
    Guid Id,
    Guid ExamGroupId,
    string ExamGroupName,
    string Name,
    string ExamType,
    Guid? AcademicPeriodId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MinPassPercent,
    Guid? GradeScaleId,
    ExamStatus Status,
    string StatusLabel,
    bool ResultDeclared,
    string? Description,
    IList<Guid> ClassIds,
    IList<ExamClassInfoDto> Classes,
    IList<ExamMarkComponentDto> Components
);

public record SaveExamRequestDto(
    Guid ExamGroupId,
    string Name,
    string ExamType,
    Guid? AcademicPeriodId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MinPassPercent,
    Guid? GradeScaleId,
    string? Description,
    IList<Guid> ClassIds,
    IList<ExamMarkComponentDto> Components
);

public record UpdateExamStatusRequestDto(ExamStatus Status);

public record ExamStatsDto(
    int Total,
    int Ongoing,
    int Completed,
    int Upcoming
);

// ── Schedule ─────────────────────────────────────────────────

public record ExamScheduleItemDto(
    Guid Id,
    Guid ExamId,
    string ExamName,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    DateOnly ExamDate,
    string? StartTime,
    string? EndTime,
    string? RoomNo,
    Guid? InvigilatorId,
    string? InvigilatorName,
    decimal MaxMarks,
    string Status
);

public record SaveExamScheduleRequestDto(
    Guid ExamId,
    Guid ClassId,
    Guid SubjectId,
    DateOnly ExamDate,
    string? StartTime,
    string? EndTime,
    string? RoomNo,
    Guid? InvigilatorId
);

// ── Marks entry ──────────────────────────────────────────────

public record ExamComponentMarkDto(
    Guid ComponentId,
    decimal? MarksObtained
);

public record ExamStudentMarksRowDto(
    Guid StudentId,
    string StudentName,
    string RollNo,
    bool IsAbsent,
    string? Remark,
    IList<ExamComponentMarkDto> Marks
);

public record ExamMarksGridDto(
    Guid ExamScheduleId,
    Guid ExamId,
    string ExamName,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    decimal MinPassPercent,
    IList<ExamMarkComponentDto> Components,
    IList<ExamStudentMarksRowDto> Students
);

public record SaveStudentMarksDto(
    Guid StudentId,
    bool IsAbsent,
    string? Remark,
    IList<ExamComponentMarkDto> Marks
);

public record SaveExamMarksRequestDto(
    Guid ExamScheduleId,
    IList<SaveStudentMarksDto> Students
);

public record ExamSubjectProgressDto(
    Guid ExamScheduleId,
    Guid SubjectId,
    string SubjectName,
    int Entered,
    int Total
);

// ── Results ──────────────────────────────────────────────────

public record ExamResultSubjectMarkDto(
    Guid SubjectId,
    decimal? Marks,
    bool IsAbsent,
    bool Pass
);

public record ExamResultRowDto(
    Guid StudentId,
    string StudentName,
    string RollNo,
    int Rank,
    decimal TotalMarks,
    decimal MaxMarks,
    decimal Percentage,
    string? Grade,
    ExamResultStatus Result,
    string ResultLabel,
    IList<ExamResultSubjectMarkDto> SubjectMarks
);

public record ExamResultSubjectColumnDto(
    Guid SubjectId,
    string SubjectName,
    decimal MaxMarks
);

public record ExamResultSheetDto(
    Guid ExamId,
    string ExamName,
    Guid ClassId,
    string ClassName,
    bool ResultDeclared,
    int TotalStudents,
    int PassCount,
    int FailCount,
    int AbsentCount,
    decimal ClassAveragePercent,
    decimal TopScore,
    decimal MaxMarks,
    IList<ExamResultSubjectColumnDto> Subjects,
    IList<ExamResultRowDto> Rows
);

public record CalculateExamResultRequestDto(Guid ExamId, Guid ClassId);

public record DeclareExamResultRequestDto(Guid ExamId, Guid ClassId);

// ── Report card ──────────────────────────────────────────────

public record ReportCardSubjectRowDto(
    string SubjectName,
    decimal MaxMarks,
    decimal? MarksObtained,
    decimal Percentage,
    string? Grade,
    bool IsAbsent,
    bool Pass
);

public record ReportCardDto(
    Guid ExamId,
    string ExamName,
    string ExamType,
    string AcademicYearTitle,
    Guid StudentId,
    string StudentName,
    string RollNo,
    string ClassName,
    decimal TotalMarks,
    decimal MaxMarks,
    decimal Percentage,
    string? Grade,
    int Rank,
    int TotalStudents,
    ExamResultStatus Result,
    string ResultLabel,
    IList<ReportCardSubjectRowDto> Subjects
);

// ── Hall tickets ─────────────────────────────────────────────

public record HallTicketScheduleDto(
    string SubjectName,
    DateOnly ExamDate,
    string? StartTime,
    string? EndTime,
    string? RoomNo
);

public record HallTicketDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string RollNo,
    string ClassName,
    string TicketNo,
    string? SeatNo,
    Guid ExamId,
    string ExamName,
    DateOnly StartDate,
    DateOnly EndDate,
    IList<HallTicketScheduleDto> Schedule
);

public record GenerateHallTicketsRequestDto(Guid ExamId, Guid ClassId);
