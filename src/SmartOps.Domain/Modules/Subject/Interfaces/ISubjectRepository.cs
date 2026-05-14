using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject.Models;

namespace SmartOps.Domain.Modules.Subject.Interfaces;

public interface ISubjectRepository
{
    Task<Guid> CreateSubjectAsync(SubjectEntity subject, CancellationToken cancellationToken);
    Task<PagedResult<SubjectListModel>> GetAllSubjectsAsync(
        int pageIndex, 
        int pageSize, 
        string? searchTerm, 
        string? sortColumn, 
        string? sortDirection, 
        string? filter, 
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DropdownDto>> GetSubjectDropdownAsync(CancellationToken cancellationToken);
    Task<SubjectEntity?> GetSubjectByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateSubjectAsync(SubjectEntity subject, CancellationToken cancellationToken);
    Task DeleteSubjectAsync(Guid id, CancellationToken cancellationToken);
}
