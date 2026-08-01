namespace SmartOps.Infrastructure.Modules.Salary;

internal static class SalaryDepartmentSql
{
    internal static string DepartmentSubquery(string schema, string employeesTableAlias) => $"""
        COALESCE((
            SELECT d.name
            FROM {schema}.departments d
            WHERE d.id = {employeesTableAlias}.departmentid
              AND d.isactive = true
            LIMIT 1
        ), '')
        """;
}
