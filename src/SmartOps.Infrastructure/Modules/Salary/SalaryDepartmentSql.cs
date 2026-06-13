namespace SmartOps.Infrastructure.Modules.Salary;

internal static class SalaryDepartmentSql
{
    internal static string DepartmentSubquery(string schema, string employeesTableAlias) => $"""
        COALESCE((
            SELECT d.name
            FROM {schema}.classsubjectteachermappings m
            INNER JOIN {schema}.employees t ON t.id = m.employeeid
            LEFT JOIN {schema}.departments d ON d.id = t.departmentid AND d.isactive = true
            WHERE m.employeeid = {employeesTableAlias}.id
              AND m.isactive = true
              AND d.name IS NOT NULL
            LIMIT 1
        ), '')
        """;
}
