using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Student.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Student;

public sealed class StudentFeeHeadAssignmentRepository : BaseRepository, IStudentFeeHeadAssignmentRepository
{
    public StudentFeeHeadAssignmentRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<IReadOnlySet<Guid>?> GetIncludedFeeHeadIdsAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT feeheadid AS FeeHeadId, isincluded AS IsIncluded
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentFeeHeadAssignments}
            WHERE studentid = @StudentId
              AND feestructureid = @FeeStructureId
              AND isactive = true;
            """;

        IEnumerable<AssignmentRow> rows = await connection
            .QueryAsync<AssignmentRow>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);

        IList<AssignmentRow> list = rows.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return list.Where(r => r.IsIncluded).Select(r => r.FeeHeadId).ToHashSet();
    }

    private sealed class AssignmentRow
    {
        public Guid FeeHeadId { get; init; }
        public bool IsIncluded { get; init; }
    }
}
