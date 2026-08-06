using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Abstractions.Storage;
using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.StaffAttendance;
using SmartOps.Domain.Modules.StaffAttendance.Entities;
using SmartOps.Infrastructure.Modules.StaffAttendance;

namespace SmartOps.Infrastructure.Modules.StaffAttendance.Services;

public sealed class StaffAttendanceService : IStaffAttendanceService
{
    private const string FaceBlobContainer = "employee-faces";
    private static readonly TimeSpan FallbackDefaultWorkingDuration = TimeSpan.FromHours(8);

    private readonly IStaffAttendanceRepository _attendanceRepo;
    private readonly IEmployeeFaceEnrollmentRepository _faceRepo;
    private readonly IFaceRecognitionClient _faceClient;
    private readonly IEmployeeAttendanceSettingsService _settingsService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAcademicCalendarService _calendarService;
    private readonly IBranchContext _branchContext;
    private readonly FaceServiceOptions _faceOptions;
    private readonly ILogger<StaffAttendanceService> _logger;

    public StaffAttendanceService(
        IStaffAttendanceRepository attendanceRepo,
        IEmployeeFaceEnrollmentRepository faceRepo,
        IFaceRecognitionClient faceClient,
        IEmployeeAttendanceSettingsService settingsService,
        IBlobStorageService blobStorage,
        ICurrentUserService currentUser,
        ITenantProvider tenantProvider,
        IAcademicCalendarService calendarService,
        IBranchContext branchContext,
        IOptions<FaceServiceOptions> faceOptions,
        ILogger<StaffAttendanceService> logger)
    {
        _attendanceRepo = attendanceRepo;
        _faceRepo = faceRepo;
        _faceClient = faceClient;
        _settingsService = settingsService;
        _blobStorage = blobStorage;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
        _calendarService = calendarService;
        _branchContext = branchContext;
        _faceOptions = faceOptions.Value;
        _logger = logger;
    }

    public async Task<Result<EmployeeAttendanceTypeSettingDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return Result<EmployeeAttendanceTypeSettingDto>.Failure("School context is not available.");
        }

        EmployeeAttendanceTypeSettingDto dto = await _settingsService.GetTypeAsync(schoolId, ct).ConfigureAwait(false);
        return Result<EmployeeAttendanceTypeSettingDto>.Success(dto);
    }

    public async Task<Result<IList<StaffAttendanceRowDto>>> ListByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        IList<StaffAttendanceListRow> rows = await _attendanceRepo.ListByDateAsync(date, ct).ConfigureAwait(false);
        IList<StaffAttendanceRowDto> dtos = rows.Select(MapListRow).ToList();
        return Result<IList<StaffAttendanceRowDto>>.Success(dtos);
    }

    public async Task<Result<StaffAttendanceRowDto?>> GetMyTodayAsync(CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        Guid? employeeId = await _attendanceRepo.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (!employeeId.HasValue)
        {
            return Result<StaffAttendanceRowDto?>.Failure("No employee profile linked to your account.");
        }

        DateOnly today = SchoolLocalTime.Today(null);
        StaffAttendanceEntity? entity = await _attendanceRepo
            .GetByEmployeeAndDateAsync(employeeId.Value, today, ct)
            .ConfigureAwait(false);
        EmployeeShiftInfo? info = await _attendanceRepo.GetEmployeeInfoAsync(employeeId.Value, ct).ConfigureAwait(false);
        if (info is null)
        {
            return Result<StaffAttendanceRowDto?>.Failure("Employee not found.");
        }

        if (entity is null)
        {
            return Result<StaffAttendanceRowDto?>.Success(new StaffAttendanceRowDto(
                Guid.Empty,
                info.Id,
                info.EmployeeName,
                info.DepartmentId,
                info.DepartmentName,
                today,
                null,
                null,
                null,
                null,
                StaffAttendanceStatus.Absent,
                StaffAttendanceStatus.Absent.ToDisplayString(),
                null,
                null,
                null,
                info.IsFaceEnrolled,
                info.PhotoUrl,
                info.ShiftStartTime));
        }

        return Result<StaffAttendanceRowDto?>.Success(MapEntity(entity, info));
    }

    public async Task<Result<StaffAttendanceRowDto>> ManualPunchAsync(
        ManualPunchRequestDto request,
        CancellationToken ct = default)
    {
        if (!StaffAttendancePunchTypes.IsValid(request.PunchType))
        {
            return Result<StaffAttendanceRowDto>.Failure("PunchType must be 'checkin' or 'checkout'.");
        }

        Result<EmployeeAttendanceTypeSettingDto> settings = await GetSettingsAsync(ct).ConfigureAwait(false);
        if (!settings.IsSuccess)
        {
            return Result<StaffAttendanceRowDto>.Failure(settings.Error!);
        }

        if (!settings.Value!.AllowsManual)
        {
            return Result<StaffAttendanceRowDto>.Failure("Manual attendance is not enabled for this school.");
        }

        Guid userId = RequireUserId();
        Result<Guid> employeeResult = await ResolvePunchEmployeeIdAsync(request.EmployeeId, userId, ct)
            .ConfigureAwait(false);
        if (!employeeResult.IsSuccess)
        {
            return Result<StaffAttendanceRowDto>.Failure(employeeResult.Error!);
        }

        Guid employeeId = employeeResult.Value!;
        DateOnly date = request.AttendanceDate ?? SchoolLocalTime.Today(null);
        DateTime punchTime = request.PunchType.Equals(StaffAttendancePunchTypes.CheckOut, StringComparison.OrdinalIgnoreCase)
            ? (request.CheckOutTime ?? SchoolLocalTime.NowDateTime())
            : (request.CheckInTime ?? SchoolLocalTime.NowDateTime());

        return await ApplyPunchAsync(
                employeeId,
                date,
                StaffAttendancePunchTypes.Normalize(request.PunchType),
                punchTime,
                StaffAttendanceSources.Manual,
                confidence: null,
                request.Remarks,
                ct)
            .ConfigureAwait(false);
    }

    public async Task<Result<StaffAttendanceRowDto>> UpdateAsync(
        Guid id,
        UpdateStaffAttendanceRequestDto request,
        CancellationToken ct = default)
    {
        StaffAttendanceEntity? entity = await _attendanceRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<StaffAttendanceRowDto>.Failure("Staff attendance record not found.");
        }

        if (request.CheckInTime.HasValue)
        {
            entity.CheckInTime = request.CheckInTime;
        }

        if (request.CheckOutTime.HasValue)
        {
            entity.CheckOutTime = request.CheckOutTime;
        }

        if (request.Remarks is not null)
        {
            entity.Remarks = request.Remarks;
        }

        EmployeeShiftInfo? info = await _attendanceRepo.GetEmployeeInfoAsync(entity.EmployeeId, ct).ConfigureAwait(false);
        TimeSpan defaultWorking = await ResolveDefaultWorkingDurationAsync(ct).ConfigureAwait(false);
        entity.Status = request.Status
            ?? ComputeStatus(
                entity.CheckInTime,
                entity.CheckOutTime,
                info?.ShiftStartTime,
                info?.ShiftEndTime,
                defaultWorking);
        entity.MarkedByUserId = RequireUserId();

        await _attendanceRepo.UpdateAsync(entity, ct).ConfigureAwait(false);

        info ??= await _attendanceRepo.GetEmployeeInfoAsync(entity.EmployeeId, ct).ConfigureAwait(false);
        if (info is null)
        {
            return Result<StaffAttendanceRowDto>.Failure("Employee not found.");
        }

        return Result<StaffAttendanceRowDto>.Success(MapEntity(entity, info));
    }

    public async Task<Result> EnrollFaceAsync(
        Guid? employeeId,
        byte[] imageBytes,
        string contentType,
        string? fileName,
        CancellationToken ct = default)
    {
        Result<EmployeeAttendanceTypeSettingDto> settings = await GetSettingsAsync(ct).ConfigureAwait(false);
        if (!settings.IsSuccess)
        {
            return Result.Failure(settings.Error!);
        }

        if (!settings.Value!.AllowsFace)
        {
            return Result.Failure("Face attendance is not enabled for this school.");
        }

        Guid userId = RequireUserId();
        Result<Guid> employeeResult = await ResolvePunchEmployeeIdAsync(employeeId, userId, ct).ConfigureAwait(false);
        if (!employeeResult.IsSuccess)
        {
            return Result.Failure(employeeResult.Error!);
        }

        Guid resolvedEmployeeId = employeeResult.Value!;
        EmployeeShiftInfo? info = await _attendanceRepo.GetEmployeeInfoAsync(resolvedEmployeeId, ct).ConfigureAwait(false);
        if (info is null)
        {
            return Result.Failure("Employee not found.");
        }

        FaceEmbedResult embed;
        try
        {
            embed = await _faceClient.EnrollEmbeddingAsync(imageBytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face enrollment embedding failed for employee {EmployeeId}", resolvedEmployeeId);
            return Result.Failure("Unable to create face embedding from the provided image.");
        }

        string extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GuessExtension(contentType);
        }

        string blobName = $"{resolvedEmployeeId}/face{extension}";
        string photoUrl;
        await using (var stream = new MemoryStream(imageBytes))
        {
            photoUrl = await _blobStorage
                .UploadFileAsync(FaceBlobContainer, blobName, stream, contentType, ct)
                .ConfigureAwait(false);
        }

        var enrollment = new EmployeeFaceEnrollmentEntity
        {
            EmployeeId = resolvedEmployeeId,
            Embedding = embed.Embedding,
            ModelName = embed.Model,
            PhotoUrl = photoUrl
        };

        await _faceRepo.UpsertAsync(enrollment, ct).ConfigureAwait(false);
        await _attendanceRepo.UpdateEmployeePhotoUrlAsync(resolvedEmployeeId, photoUrl, ct).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<StaffAttendanceRowDto>> FacePunchAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        Result<EmployeeAttendanceTypeSettingDto> settings = await GetSettingsAsync(ct).ConfigureAwait(false);
        if (!settings.IsSuccess)
        {
            return Result<StaffAttendanceRowDto>.Failure(settings.Error!);
        }

        if (!settings.Value!.AllowsFace)
        {
            return Result<StaffAttendanceRowDto>.Failure("Face attendance is not enabled for this school.");
        }

        FaceEmbedResult embed;
        try
        {
            embed = await _faceClient.EnrollEmbeddingAsync(imageBytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face punch embedding failed");
            return Result<StaffAttendanceRowDto>.Failure("Unable to create face embedding from the provided image.");
        }

        IList<EmployeeFaceEnrollmentEntity> enrollments = await _faceRepo.ListActiveForTenantAsync(ct)
            .ConfigureAwait(false);
        if (enrollments.Count == 0)
        {
            return Result<StaffAttendanceRowDto>.Failure("No face enrollments found for this school.");
        }

        IReadOnlyList<FaceMatchCandidate> candidates = enrollments
            .Select(e => new FaceMatchCandidate(e.EmployeeId, e.Embedding))
            .ToList();

        FaceMatchResult? match;
        try
        {
            match = await _faceClient.MatchAsync(embed.Embedding, candidates, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face punch match failed");
            return Result<StaffAttendanceRowDto>.Failure("Face matching failed.");
        }

        if (match is null || match.Score < _faceOptions.MatchThreshold)
        {
            return Result<StaffAttendanceRowDto>.Failure("No matching employee face found.");
        }

        DateOnly today = SchoolLocalTime.Today(null);
        StaffAttendanceEntity? existing = await _attendanceRepo
            .GetByEmployeeAndDateAsync(match.EmployeeId, today, ct)
            .ConfigureAwait(false);

        string punchType = existing?.CheckInTime is null
            ? StaffAttendancePunchTypes.CheckIn
            : StaffAttendancePunchTypes.CheckOut;

        return await ApplyPunchAsync(
                match.EmployeeId,
                today,
                punchType,
                SchoolLocalTime.NowDateTime(),
                StaffAttendanceSources.Face,
                match.Score,
                remarks: null,
                ct)
            .ConfigureAwait(false);
    }

    public async Task<Result> DeactivateFaceEnrollmentAsync(Guid employeeId, CancellationToken ct = default)
    {
        EmployeeShiftInfo? info = await _attendanceRepo.GetEmployeeInfoAsync(employeeId, ct).ConfigureAwait(false);
        if (info is null)
        {
            return Result.Failure("Employee not found.");
        }

        await _faceRepo.DeactivateAsync(employeeId, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<StaffAttendanceReportDto>> GetReportAsync(
        int month,
        int year,
        Guid? departmentId,
        CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result<StaffAttendanceReportDto>.Failure("Month must be between 1 and 12.");
        }

        if (year < 2000 || year > 2100)
        {
            return Result<StaffAttendanceReportDto>.Failure("Year is out of range.");
        }

        IList<StaffAttendanceReportSourceRow> source = await _attendanceRepo
            .GetReportSourceAsync(month, year, departmentId, ct)
            .ConfigureAwait(false);

        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        int totalWorkingDays = await _calendarService
            .CountWorkingDaysAsync(_branchContext.ActiveBranchId, year, month, CalendarAudience.Staff, ct)
            .ConfigureAwait(false);

        var employees = source
            .GroupBy(r => r.EmployeeId)
            .Select(g =>
            {
                var daily = new Dictionary<int, string>();
                int present = 0, absent = 0, late = 0, half = 0;

                foreach (StaffAttendanceReportSourceRow row in g.Where(x => x.AttendanceDate.HasValue && x.Status.HasValue))
                {
                    int day = row.AttendanceDate!.Value.Day;
                    StaffAttendanceStatus status = row.Status!.Value;
                    daily[day] = status.ToReportCode();
                    switch (status)
                    {
                        case StaffAttendanceStatus.Present: present++; break;
                        case StaffAttendanceStatus.Absent: absent++; break;
                        case StaffAttendanceStatus.Late: late++; break;
                        case StaffAttendanceStatus.HalfDay: half++; break;
                    }
                }

                StaffAttendanceReportSourceRow first = g.First();
                return new StaffAttendanceReportEmployeeDto(
                    first.EmployeeId,
                    first.EmployeeName,
                    first.DepartmentName,
                    present,
                    absent,
                    late,
                    half,
                    daily);
            })
            .OrderBy(e => e.EmployeeName)
            .ToList();

        return Result<StaffAttendanceReportDto>.Success(
            new StaffAttendanceReportDto(month, year, departmentId, totalWorkingDays, employees));
    }

    public async Task<Result<MyMonthAttendanceDto>> GetMyMonthAsync(
        int month,
        int year,
        CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result<MyMonthAttendanceDto>.Failure("Month must be between 1 and 12.");
        }

        if (year < 2000 || year > 2100)
        {
            return Result<MyMonthAttendanceDto>.Failure("Year is out of range.");
        }

        Guid userId = RequireUserId();
        Guid? employeeId = await _attendanceRepo.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (!employeeId.HasValue)
        {
            return Result<MyMonthAttendanceDto>.Failure("No employee profile linked to your account.");
        }

        IList<StaffAttendanceDayStatusRow> rows = await _attendanceRepo
            .GetEmployeeMonthStatusesAsync(employeeId.Value, month, year, ct)
            .ConfigureAwait(false);

        var daily = new Dictionary<int, string>();
        int present = 0, absent = 0, late = 0, half = 0;
        foreach (StaffAttendanceDayStatusRow row in rows)
        {
            daily[row.AttendanceDate.Day] = row.Status.ToReportCode();
            switch (row.Status)
            {
                case StaffAttendanceStatus.Present: present++; break;
                case StaffAttendanceStatus.Absent: absent++; break;
                case StaffAttendanceStatus.Late: late++; break;
                case StaffAttendanceStatus.HalfDay: half++; break;
            }
        }

        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        IReadOnlySet<int> nonWorkingDays = await _calendarService
            .GetNonWorkingDayNumbersAsync(_branchContext.ActiveBranchId, year, month, CalendarAudience.Staff, ct)
            .ConfigureAwait(false);
        int totalWorkingDays = await _calendarService
            .CountWorkingDaysAsync(_branchContext.ActiveBranchId, year, month, CalendarAudience.Staff, ct)
            .ConfigureAwait(false);

        return Result<MyMonthAttendanceDto>.Success(new MyMonthAttendanceDto(
            month,
            year,
            present,
            absent,
            late,
            half,
            totalWorkingDays,
            daily,
            nonWorkingDays.OrderBy(d => d).ToList()));
    }

    private async Task<Result<StaffAttendanceRowDto>> ApplyPunchAsync(
        Guid employeeId,
        DateOnly date,
        string punchType,
        DateTime punchTime,
        string source,
        float? confidence,
        string? remarks,
        CancellationToken ct)
    {
        EmployeeShiftInfo? info = await _attendanceRepo.GetEmployeeInfoAsync(employeeId, ct).ConfigureAwait(false);
        if (info is null)
        {
            return Result<StaffAttendanceRowDto>.Failure("Employee not found.");
        }

        StaffAttendanceEntity? existing = await _attendanceRepo
            .GetByEmployeeAndDateAsync(employeeId, date, ct)
            .ConfigureAwait(false);

        bool isCheckIn = punchType.Equals(StaffAttendancePunchTypes.CheckIn, StringComparison.OrdinalIgnoreCase);

        if (existing is not null && existing.CheckInTime.HasValue && existing.CheckOutTime.HasValue)
        {
            return Result<StaffAttendanceRowDto>.Failure(
                "Both check-in and check-out are already recorded. Use update to modify.");
        }

        if (isCheckIn)
        {
            if (existing?.CheckInTime is not null)
            {
                return Result<StaffAttendanceRowDto>.Failure("Check-in already recorded for this date.");
            }

            if (existing is null)
            {
                existing = new StaffAttendanceEntity
                {
                    EmployeeId = employeeId,
                    AttendanceDate = date,
                    MarkedByUserId = RequireUserId()
                };
            }

            existing.CheckInTime = punchTime;
            existing.CheckInSource = source;
            existing.CheckInConfidence = confidence;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                existing.Remarks = remarks;
            }
        }
        else
        {
            if (existing is null || existing.CheckInTime is null)
            {
                return Result<StaffAttendanceRowDto>.Failure("Check-in is required before check-out.");
            }

            if (existing.CheckOutTime is not null)
            {
                return Result<StaffAttendanceRowDto>.Failure("Check-out already recorded for this date.");
            }

            existing.CheckOutTime = punchTime;
            existing.CheckOutSource = source;
            existing.CheckOutConfidence = confidence;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                existing.Remarks = remarks;
            }
        }

        existing.MarkedByUserId = RequireUserId();
        TimeSpan defaultWorking = await ResolveDefaultWorkingDurationAsync(ct).ConfigureAwait(false);
        existing.Status = ComputeStatus(
            existing.CheckInTime,
            existing.CheckOutTime,
            info.ShiftStartTime,
            info.ShiftEndTime,
            defaultWorking);

        Guid id = await _attendanceRepo.UpsertPunchAsync(existing, ct).ConfigureAwait(false);
        existing.Id = id;

        return Result<StaffAttendanceRowDto>.Success(MapEntity(existing, info));
    }

    private async Task<Result<Guid>> ResolvePunchEmployeeIdAsync(
        Guid? requestedEmployeeId,
        Guid userId,
        CancellationToken ct)
    {
        if (requestedEmployeeId.HasValue && requestedEmployeeId.Value != Guid.Empty)
        {
            EmployeeShiftInfo? info = await _attendanceRepo
                .GetEmployeeInfoAsync(requestedEmployeeId.Value, ct)
                .ConfigureAwait(false);
            return info is null
                ? Result<Guid>.Failure("Employee not found.")
                : Result<Guid>.Success(requestedEmployeeId.Value);
        }

        Guid? selfId = await _attendanceRepo.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        return selfId.HasValue
            ? Result<Guid>.Success(selfId.Value)
            : Result<Guid>.Failure("No employee profile linked to your account.");
    }

    internal static StaffAttendanceStatus ComputeStatus(
        DateTime? checkIn,
        DateTime? checkOut,
        string? shiftStartTime,
        string? shiftEndTime,
        TimeSpan defaultWorkingHours)
    {
        if (!checkIn.HasValue)
        {
            return StaffAttendanceStatus.Absent;
        }

        if (checkOut.HasValue)
        {
            TimeSpan worked = checkOut.Value - checkIn.Value;
            if (worked < TimeSpan.Zero)
            {
                worked = TimeSpan.Zero;
            }

            TimeSpan fullDay = ResolveFullDayDuration(shiftStartTime, shiftEndTime, defaultWorkingHours);
            TimeSpan halfDay = TimeSpan.FromTicks(fullDay.Ticks / 2);
            if (worked < halfDay)
            {
                return StaffAttendanceStatus.HalfDay;
            }
        }

        if (TryParseShiftTime(shiftStartTime, out TimeOnly shiftStart))
        {
            TimeOnly checkInTime = TimeOnly.FromDateTime(checkIn.Value);
            if (checkInTime > shiftStart)
            {
                return StaffAttendanceStatus.Late;
            }
        }

        return StaffAttendanceStatus.Present;
    }

    internal static TimeSpan ResolveFullDayDuration(
        string? shiftStartTime,
        string? shiftEndTime,
        TimeSpan defaultWorkingHours)
    {
        if (TryParseShiftTime(shiftStartTime, out TimeOnly start)
            && TryParseShiftTime(shiftEndTime, out TimeOnly end))
        {
            TimeSpan duration = end - start;
            if (duration <= TimeSpan.Zero)
            {
                // Overnight shift e.g. 22:00 → 06:00
                duration = duration.Add(TimeSpan.FromDays(1));
            }

            if (duration > TimeSpan.Zero)
            {
                return duration;
            }
        }

        return defaultWorkingHours > TimeSpan.Zero
            ? defaultWorkingHours
            : FallbackDefaultWorkingDuration;
    }

    private async Task<TimeSpan> ResolveDefaultWorkingDurationAsync(CancellationToken ct)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return FallbackDefaultWorkingDuration;
        }

        EmployeeAttendanceTypeSettingDto dto = await _settingsService.GetTypeAsync(schoolId, ct).ConfigureAwait(false);
        double hours = (double)dto.DefaultWorkingHours;
        if (hours <= 0)
        {
            return FallbackDefaultWorkingDuration;
        }

        return TimeSpan.FromHours(hours);
    }

    private static bool TryParseShiftTime(string? raw, out TimeOnly value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return TimeOnly.TryParse(raw, out value);
    }

    private static StaffAttendanceRowDto MapListRow(StaffAttendanceListRow row)
    {
        StaffAttendanceStatus status = row.Id == Guid.Empty
            ? StaffAttendanceStatus.Absent
            : row.Status;

        // COALESCE used 0 for missing attendance rows
        if (row.Id == Guid.Empty || (short)row.Status == 0)
        {
            status = StaffAttendanceStatus.Absent;
        }

        return new StaffAttendanceRowDto(
            row.Id,
            row.EmployeeId,
            row.EmployeeName,
            row.DepartmentId,
            row.DepartmentName,
            row.AttendanceDate,
            row.CheckInTime,
            row.CheckOutTime,
            row.CheckInSource,
            row.CheckOutSource,
            status,
            status.ToDisplayString(),
            row.Remarks,
            row.CheckInConfidence,
            row.CheckOutConfidence,
            row.IsFaceEnrolled,
            row.PhotoUrl,
            row.ShiftStartTime);
    }

    private static StaffAttendanceRowDto MapEntity(StaffAttendanceEntity entity, EmployeeShiftInfo info) =>
        new(
            entity.Id,
            entity.EmployeeId,
            info.EmployeeName,
            info.DepartmentId,
            info.DepartmentName,
            entity.AttendanceDate,
            entity.CheckInTime,
            entity.CheckOutTime,
            entity.CheckInSource,
            entity.CheckOutSource,
            entity.Status,
            entity.Status.ToDisplayString(),
            entity.Remarks,
            entity.CheckInConfidence,
            entity.CheckOutConfidence,
            info.IsFaceEnrolled,
            info.PhotoUrl,
            info.ShiftStartTime);

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return _currentUser.UserId;
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = _tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }

    private static string GuessExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
}
