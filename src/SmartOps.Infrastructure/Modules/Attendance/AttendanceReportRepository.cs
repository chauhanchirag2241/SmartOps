using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Attendance;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Attendance;

public sealed class AttendanceReportRepository : IAttendanceReportRepository
{
    private readonly DapperContext _context;
    private readonly ITenantSchemaProvider _tenantSchema;

    public AttendanceReportRepository(DapperContext context, ITenantSchemaProvider tenantSchema)
    {
        _context = context;
        _tenantSchema = tenantSchema;
    }

    private string OperationalSchema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    public async Task<AttendanceReportResponseDto> GetMonthlyAttendanceReportAsync(
        Guid classId,
        int month,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        // First, get the academic year to figure out the actual year for the requested month
        string aySql = $"SELECT startdate, enddate FROM {OperationalSchema}.{DatabaseConfig.TableAcademicYears} WHERE id = @AyId";
        var ay = await connection.QuerySingleOrDefaultAsync<dynamic>(aySql, new { AyId = academicYearId });
        
        if (ay == null)
            throw new Exception("Academic year not found");

        DateTime ayStartDate = (DateTime)ay.startdate;
        DateTime ayEndDate = (DateTime)ay.enddate;
        
        int year = ayStartDate.Year;
        // If the month is less than the start date's month, it must be the next calendar year
        if (month < ayStartDate.Month && ayEndDate.Year > ayStartDate.Year)
        {
            year = ayEndDate.Year;
        }

        // Calculate days in month
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateOnly startDate = new DateOnly(year, month, 1);
        DateOnly endDate = new DateOnly(year, month, daysInMonth);
        
        // Count working days (excluding Sundays for this simple implementation)
        int totalWorkingDays = 0;
        for (int i = 1; i <= daysInMonth; i++)
        {
            if (new DateTime(year, month, i).DayOfWeek != DayOfWeek.Sunday)
            {
                totalWorkingDays++;
            }
        }

        // Fetch Class Name
        string classSql = $"""
            SELECT classname 
            FROM {OperationalSchema}.{DatabaseConfig.TableClasses} 
            WHERE id = @ClassId AND isactive = true;
            """;
        
        string className = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(classSql, new { ClassId = classId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false) ?? "Unknown Class";

        // Query students in this class
        string studentsSql = $"""
            SELECT 
                s.id as StudentId,
                s.firstname as FirstName,
                s.lastname as LastName,
                sa.rollnumber as RollNo
            FROM {OperationalSchema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {OperationalSchema}.{DatabaseConfig.TableStudentAcademics} sa ON s.id = sa.studentid
            WHERE sa.classid = @ClassId AND s.isactive = true AND sa.isactive = true;
            """;

        var students = await connection.QueryAsync<StudentDto>(
            new CommandDefinition(studentsSql, new { ClassId = classId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // Query attendance records for the month
        string attendanceSql = $"""
            SELECT 
                studentid as StudentId,
                attendancedate as AttendanceDate,
                status as Status
            FROM {OperationalSchema}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId 
              AND attendancedate >= @StartDate 
              AND attendancedate <= @EndDate
              AND isactive = true;
            """;

        var attendanceRecords = await connection.QueryAsync<AttendanceRecordDto>(
            new CommandDefinition(attendanceSql, new { ClassId = classId, StartDate = startDate, EndDate = endDate }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // Group attendance by student
        var attendanceLookup = attendanceRecords
            .GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var studentDtos = new List<AttendanceReportStudentDto>();

        int perfectAttendanceCount = 0;
        int below75Count = 0;
        int chronicAbsenteesCount = 0;
        decimal totalClassPercentage = 0;
        int validStudentsForAverage = 0;

        foreach (var student in students)
        {
            Guid studentId = student.StudentId;
            string fullName = $"{student.FirstName} {student.LastName}".Trim();
            string initials = fullName.Length >= 2 ? fullName.Substring(0, 2).ToUpper() : "ST";
            
            var records = attendanceLookup.GetValueOrDefault(studentId, new List<AttendanceRecordDto>());

            int present = 0, absent = 0, leave = 0, late = 0;
            var dailyStatus = new Dictionary<int, string>();

            foreach (var record in records)
            {
                DateOnly date = record.AttendanceDate;
                string status = record.Status switch {
                    1 => "P",
                    2 => "A",
                    3 => "L",
                    4 => "late",
                    _ => ""
                };
                dailyStatus[date.Day] = status;

                switch (record.Status)
                {
                    case 1: present++; break;
                    case 2: absent++; break;
                    case 3: leave++; break;
                    case 4: late++; break;
                }
            }

            // Fill in missing days with "S" for holidays (Sundays) and empty for others
            for (int i = 1; i <= daysInMonth; i++)
            {
                if (!dailyStatus.ContainsKey(i))
                {
                    if (new DateTime(year, month, i).DayOfWeek == DayOfWeek.Sunday)
                    {
                        dailyStatus[i] = "S";
                    }
                    else
                    {
                        dailyStatus[i] = ""; // Not marked
                    }
                }
            }

            decimal percentage = 0;
            if (totalWorkingDays > 0)
            {
                // Present + Late counts as attended
                percentage = Math.Round((decimal)(present + late) / totalWorkingDays * 100, 1);
            }

            if (percentage == 100) perfectAttendanceCount++;
            if (percentage < 75) below75Count++;
            if (absent >= 3) chronicAbsenteesCount++;

            totalClassPercentage += percentage;
            validStudentsForAverage++;

            studentDtos.Add(new AttendanceReportStudentDto(
                StudentId: studentId,
                StudentName: fullName,
                RollNo: student.RollNo ?? "",
                AvatarInitials: initials,
                TotalPresent: present,
                TotalAbsent: absent,
                TotalLeave: leave,
                TotalLate: late,
                AttendancePercentage: percentage,
                DailyStatus: dailyStatus
            ));
        }

        decimal classAverage = validStudentsForAverage > 0 
            ? Math.Round(totalClassPercentage / validStudentsForAverage, 1) 
            : 0;

        // Sort students by Roll Number (handle numeric sort if possible)
        studentDtos = studentDtos.OrderBy(s => {
            if (int.TryParse(s.RollNo, out int num)) return num.ToString("D4");
            return s.RollNo;
        }).ToList();

        return new AttendanceReportResponseDto(
            ClassId: classId,
            ClassName: className,
            Month: month,
            Year: year,
            TotalWorkingDays: totalWorkingDays,
            ClassAveragePercentage: classAverage,
            StudentsWithPerfectAttendance: perfectAttendanceCount,
            StudentsBelow75Percent: below75Count,
            ChronicAbsentees: chronicAbsenteesCount,
            Students: studentDtos
        );
    }

    private class StudentDto
    {
        public Guid StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? RollNo { get; set; }
    }

    private class AttendanceRecordDto
    {
        public Guid StudentId { get; set; }
        public DateOnly AttendanceDate { get; set; }
        public int Status { get; set; }
    }
}
