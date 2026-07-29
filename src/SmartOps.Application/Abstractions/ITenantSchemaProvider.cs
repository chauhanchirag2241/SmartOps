namespace SmartOps.Application.Abstractions;

public interface ITenantSchemaProvider
{
    /// <summary>
    /// Schema for school operational data (<c>school</c> on dedicated DB).
    /// </summary>
    string GetOperationalSchema();

    /// <summary>
    /// Schema for identity/management tables: <c>man</c> on dedicated school DB, else platform <c>global</c>.
    /// </summary>
    string GetIdentitySchema();

    bool IsTenantScoped { get; }
}
