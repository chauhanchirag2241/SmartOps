using Npgsql;

namespace SmartOps.Infrastructure.MultiTenancy;

internal static class SchoolDatabaseConnectionBuilder
{
    public static string BuildDatabaseName(string prefix, string subdomain)
    {
        string slug = subdomain.Trim().ToLowerInvariant().Replace('-', '_');
        foreach (char c in slug)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                throw new ArgumentException($"Subdomain '{subdomain}' contains invalid database name characters.");
            }
        }

        return $"{prefix}{slug}";
    }

    public static string BuildConnectionString(string platformConnectionString, string databaseName)
    {
        NpgsqlConnectionStringBuilder builder = new(platformConnectionString)
        {
            Database = databaseName,
            MaxPoolSize = 20,
            Timeout = 15
        };
        return builder.ConnectionString;
    }

    public static string BuildAdminConnectionString(string platformConnectionString)
    {
        NpgsqlConnectionStringBuilder builder = new(platformConnectionString)
        {
            Database = "postgres"
        };
        return builder.ConnectionString;
    }
}
