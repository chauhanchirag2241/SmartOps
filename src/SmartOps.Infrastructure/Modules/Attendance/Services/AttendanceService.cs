using Microsoft.Extensions.Logging;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Attendance.DTOs;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Domain.Modules.Attendance.Enums;
using SmartOps.Shared.Common;
using SmartOps.Shared.Configuration;
using AttendanceEntity = SmartOps.Domain.Modules.Attendance.Entities.Attendance;

namespace SmartOps.Infrastructure.Modules.Attendance.Services;

public sealed class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IAttendanceRepository attendanceRepo,
        ICurrentUserService currentUser,
        ILogger<AttendanceService> logger)
    {
        _attendanceRepo = attendanceRepo;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<ClassAttendanceResponseDto>> GetClassAttendanceAsync(
        GetClassAttendanceRequestDto request,
        CancellationToken ct = default)
    {
        var existing =
            await _attendanceRepo.GetByClassAndDateAsync(
                request.ClassId, request.AttendanceDate, ct)
            .ConfigureAwait(false);

        var students = existing
            .Select(a => a.ToDto())
            .ToList();

        var response = BuildClassResponse(
            request.ClassId,
            request.AttendanceDate,
            existing.Count > 0,
            students);

        return Result<ClassAttendanceResponseDto>.Success(response);
    }

    public async Task<Result<ClassAttendanceResponseDto>> SubmitAttendanceAsync(
        SubmitAttendanceRequestDto request,
        CancellationToken ct = default)
    {
        var teacherId = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId
            : Guid.Parse(DatabaseConfig.SystemUserId);

        var records = request.Students
            .Select(s => new AttendanceEntity
            {
                Id = Guid.NewGuid(),
                ClassId = request.ClassId,
                StudentId = s.StudentId,
                TeacherId = teacherId,
                AttendanceDate = request.AttendanceDate,
                Status = s.Status,
                Remarks = s.Remarks,
            })
            .ToList();

        await _attendanceRepo.BulkUpsertAsync(records, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Attendance submitted by {TeacherId} for class {ClassId} on {Date}. Count: {Count}",
            teacherId,
            request.ClassId,
            request.AttendanceDate,
            records.Count);

        return await GetClassAttendanceAsync(
            new GetClassAttendanceRequestDto(request.ClassId, request.AttendanceDate),
            ct)
            .ConfigureAwait(false);
    }

    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentSummaryAsync(
        Guid studentId,
        int month,
        int year,
        CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result<StudentAttendanceSummaryDto>.Failure("Month must be between 1 and 12.");
        }

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var records =
            await _attendanceRepo.GetByStudentAndRangeAsync(
                studentId, from, to, ct)
            .ConfigureAwait(false);

        var total = records.Count;
        var present = records.Count(r => r.Status == AttendanceStatus.Present);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var leave = records.Count(r => r.Status == AttendanceStatus.Leave);
        var late = records.Count(r => r.Status == AttendanceStatus.Late);

        var percentage = total > 0
            ? Math.Round((decimal)(present + late) / total * 100, 2)
            : 0;

        var response = new StudentAttendanceSummaryDto(
            studentId,
            string.Empty,
            month,
            year,
            total,
            present,
            absent,
            leave,
            late,
            percentage);

        return Result<StudentAttendanceSummaryDto>.Success(response);
    }

    private static ClassAttendanceResponseDto BuildClassResponse(
        Guid classId,
        DateOnly attendanceDate,
        bool isSubmitted,
        IList<AttendanceResponseDto> students)
    {
        return new ClassAttendanceResponseDto(
            classId,
            string.Empty,
            attendanceDate,
            students.Count,
            students.Count(s => s.Status == AttendanceStatus.Present),
            students.Count(s => s.Status == AttendanceStatus.Absent),
            students.Count(s => s.Status == AttendanceStatus.Leave),
            students.Count(s => s.Status == AttendanceStatus.Late),
            isSubmitted,
            students);
    }
}
