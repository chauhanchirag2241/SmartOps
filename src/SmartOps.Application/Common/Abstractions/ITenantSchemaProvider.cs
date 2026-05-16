namespace SmartOps.Application.Common.Abstractions;

public interface ITenantSchemaProvider
{
    /// <summary>
    /// Schema for school operational data (tenant schema or shared global when no tenant).
    /// </summary>
    string GetOperationalSchema();

    bool IsTenantScoped { get; }
}
