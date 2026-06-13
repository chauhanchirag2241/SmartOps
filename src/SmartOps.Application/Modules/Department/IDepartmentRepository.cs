namespace SmartOps.Application.Modules.Department;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<DepartmentEntity>> GetAllAsync(CancellationToken cancellationToken = default);
}
