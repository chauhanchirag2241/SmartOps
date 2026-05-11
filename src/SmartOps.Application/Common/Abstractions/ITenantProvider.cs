namespace SmartOps.Application.Common.Abstractions;

public interface ITenantProvider
{
    string? GetCurrentTenantId();

    string? GetCurrentSchoolId();
}
