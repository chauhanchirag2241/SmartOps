namespace SmartOps.Infrastructure.Modules.Salary;

internal static class SalaryDepartmentSql
{
    /// <summary>
    /// Department name when the teacher has an active class-subject mapping and a linked department; otherwise empty.
    /// </summary>
    internal static string DepartmentSubquery(string schema, string teachersTableAlias) => $"""
        COALESCE((
            SELECT d.name
            FROM {schema}.classsubjectteachermappings m
            INNER JOIN {schema}.teachers t ON t.id = m.teacherid
            LEFT JOIN {schema}.departments d ON d.id = t.departmentid AND d.isactive = true
            WHERE m.teacherid = {teachersTableAlias}.id
              AND m.isactive = true
              AND d.name IS NOT NULL
            LIMIT 1
        ), '')
        """;
}
