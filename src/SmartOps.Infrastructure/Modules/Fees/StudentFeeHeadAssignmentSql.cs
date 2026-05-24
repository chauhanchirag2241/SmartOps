using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Fees;

/// <summary>SQL fragments for per-student optional fee head selection.</summary>
internal static class StudentFeeHeadAssignmentSql
{
    /// <summary>
    /// True when the fee type applies: no assignment rows (legacy) or an active row with isincluded = true.
    /// </summary>
    public static string FeeTypeIncludedPredicate(string schema, string feeTypeIdColumn, string studentIdColumn, string versionIdColumn)
    {
        string table = $"{schema}.{DatabaseConfig.TableStudentFeeHeadAssignments}";
        return $"""
            (
              NOT EXISTS (
                SELECT 1 FROM {table} sfha
                WHERE sfha.studentid = {studentIdColumn}
                  AND sfha.feestructureversionid = {versionIdColumn}
                  AND sfha.isactive = true
              )
              OR EXISTS (
                SELECT 1 FROM {table} sfha
                WHERE sfha.studentid = {studentIdColumn}
                  AND sfha.feestructureversionid = {versionIdColumn}
                  AND sfha.feetypeid = {feeTypeIdColumn}
                  AND sfha.isincluded = true
                  AND sfha.isactive = true
              )
            )
            """;
    }
}
