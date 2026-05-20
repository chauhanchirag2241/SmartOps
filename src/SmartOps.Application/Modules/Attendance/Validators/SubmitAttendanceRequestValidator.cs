using FluentValidation;
using SmartOps.Application.Modules.Attendance;

namespace SmartOps.Application.Modules.Attendance.Validators;

public class SubmitAttendanceRequestValidator
    : AbstractValidator<SubmitAttendanceRequestDto>
{
    public SubmitAttendanceRequestValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty()
            .WithMessage("Class is required.");

        RuleFor(x => x.AttendanceDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Attendance date cannot be in the future.");

        RuleFor(x => x.Students)
            .NotEmpty()
            .WithMessage("At least one student is required.");

        RuleForEach(x => x.Students).ChildRules(s =>
        {
            s.RuleFor(x => x.StudentId)
                .NotEmpty()
                .WithMessage("Student ID is required.");

            s.RuleFor(x => x.Status)
                .IsInEnum()
                .WithMessage("Status must be a valid AttendanceStatus value (1=Present, 2=Absent, 3=Leave, 4=Late).");
        });
    }
}
