using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeaveAccrualService : ILeaveAccrualService
{
    private readonly ILeaveBalanceRepository _balanceRepo;
    private readonly ISchoolDbConnectionFactory _schoolDbFactory;
    private readonly DapperContext _context;
    private readonly ILogger<LeaveAccrualService> _logger;

    public LeaveAccrualService(
        ILeaveBalanceRepository balanceRepo,
        ISchoolDbConnectionFactory schoolDbFactory,
        DapperContext context,
        ILogger<LeaveAccrualService> logger)
    {
        _balanceRepo = balanceRepo;
        _schoolDbFactory = schoolDbFactory;
        _context = context;
        _logger = logger;
    }

    public async Task<Result> RunAllSchoolsAsync(int year, int month, CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result.Failure("Month must be between 1 and 12.");
        }

        IDbConnection platform = await _context.GetGlobalDatabaseConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id
            FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
            WHERE isactive = true
              AND connectionstring IS NOT NULL
              AND trim(connectionstring) <> '';
            """;
        IEnumerable<Guid> schoolIds = await platform.QueryAsync<Guid>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);

        var errors = new StringBuilder();
        foreach (Guid schoolId in schoolIds)
        {
            Result result = await RunForSchoolAsync(schoolId, year, month, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                errors.AppendLine($"{schoolId}: {result.Error}");
            }
        }

        return errors.Length == 0
            ? Result.Success()
            : Result.Failure(errors.ToString().Trim());
    }

    public async Task<Result> RunForSchoolAsync(Guid schoolId, int year, int month, CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result.Failure("Month must be between 1 and 12.");
        }

        string schoolSchema = DatabaseConfig.Schema_School;
        string identitySchema = DatabaseConfig.Schema_Man;
        Guid actorId = Guid.Parse(DatabaseConfig.SystemUserId);
        DateOnly txnDate = new(year, month, 1);

        IDbConnection? db = null;
        try
        {
            db = await _schoolDbFactory.OpenBySchoolIdAsync(schoolId, ct).ConfigureAwait(false);

            Guid? runId = await _balanceRepo.TryStartAccrualRunAsync(db, schoolSchema, year, month, ct)
                .ConfigureAwait(false);
            if (!runId.HasValue)
            {
                _logger.LogInformation(
                    "Leave accrual already ran for school {SchoolId} {Year}-{Month}", schoolId, year, month);
                return Result.Success();
            }

            Guid? academicYearId = await _balanceRepo.GetCurrentAcademicYearIdAsync(db, schoolSchema, ct)
                .ConfigureAwait(false);
            if (!academicYearId.HasValue)
            {
                await _balanceRepo.MarkAccrualRunAsync(
                        db, schoolSchema, runId.Value, LeaveAccrualRunStatus.Failed, 0,
                        "No current academic year", ct)
                    .ConfigureAwait(false);
                return Result.Failure($"School {schoolId}: no current academic year.");
            }

            IList<LeavePolicyEntity> policies = await _balanceRepo
                .GetActivePoliciesAsync(db, schoolSchema, ct).ConfigureAwait(false);
            IList<EmployeeUserTypeRow> employees = await _balanceRepo
                .ListActiveEmployeesWithUserTypeAsync(db, schoolSchema, identitySchema, ct)
                .ConfigureAwait(false);

            int scored = 0;
            var errorLog = new StringBuilder();

            foreach (EmployeeUserTypeRow employee in employees)
            {
                IEnumerable<LeavePolicyEntity> matched = policies.Where(p => p.UserTypeId == employee.UserTypeId);
                foreach (LeavePolicyEntity policy in matched)
                {
                    if (policy.MonthlyLeave <= 0)
                    {
                        continue;
                    }

                    try
                    {
                        await _balanceRepo.ApplyAccrualCreditAsync(
                                db,
                                schoolSchema,
                                employee.EmployeeId,
                                policy.LeaveTypeId,
                                academicYearId.Value,
                                policy.MonthlyLeave,
                                actorId,
                                txnDate,
                                $"Monthly accrual {year}-{month:D2}",
                                ct)
                            .ConfigureAwait(false);
                        scored++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Accrual failed for school {SchoolId} employee {EmployeeId} leave type {LeaveTypeId}",
                            schoolId, employee.EmployeeId, policy.LeaveTypeId);
                        errorLog.AppendLine($"{employee.EmployeeId}/{policy.LeaveTypeId}: {ex.Message}");
                    }
                }
            }

            LeaveAccrualRunStatus status = errorLog.Length == 0
                ? LeaveAccrualRunStatus.Success
                : scored > 0 ? LeaveAccrualRunStatus.Partial : LeaveAccrualRunStatus.Failed;

            await _balanceRepo.MarkAccrualRunAsync(
                    db, schoolSchema, runId.Value, status, scored,
                    errorLog.Length == 0 ? null : errorLog.ToString(), ct)
                .ConfigureAwait(false);

            return status == LeaveAccrualRunStatus.Failed
                ? Result.Failure(errorLog.ToString().Trim())
                : Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leave accrual failed for school {SchoolId}", schoolId);
            return Result.Failure($"School {schoolId}: {ex.Message}");
        }
        finally
        {
            db?.Dispose();
        }
    }
}
