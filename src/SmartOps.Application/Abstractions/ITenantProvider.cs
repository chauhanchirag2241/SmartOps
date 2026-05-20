namespace SmartOps.Application.Abstractions;

public interface ITenantProvider
{
    string? GetCurrentTenantId();

    string? GetCurrentSchoolId();
}
