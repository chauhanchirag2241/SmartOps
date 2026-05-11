using SmartOps.Application.Modules.School.DTOs;
using SmartOps.Shared.Common;

namespace SmartOps.Application.Modules.School.Interfaces;

public interface ISchoolService
{
    Task<Result<SchoolDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IList<SchoolDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<SchoolDto>> CreateAsync(CreateSchoolDto dto, CancellationToken cancellationToken = default);

    Task<Result<SchoolDto>> UpdateAsync(Guid id, UpdateSchoolDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
