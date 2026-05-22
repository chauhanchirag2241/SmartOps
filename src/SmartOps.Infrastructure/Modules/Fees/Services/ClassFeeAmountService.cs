using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class ClassFeeAmountService : IClassFeeAmountService
{
    private readonly IClassFeeAmountRepository _repo;
    private readonly IFeeStructureRepository _feeStructureRepo;

    public ClassFeeAmountService(IClassFeeAmountRepository repo, IFeeStructureRepository feeStructureRepo)
    {
        _repo = repo;
        _feeStructureRepo = feeStructureRepo;
    }

    public async Task<Result<IList<ClassFeeSummaryDto>>> GetClassSummariesAsync(Guid academicYearId, CancellationToken ct = default)
    {
        if (academicYearId == Guid.Empty)
        {
            return Result<IList<ClassFeeSummaryDto>>.Failure("Academic year is required.");
        }

        IList<ClassFeeSummaryRow> rows = await _repo.GetClassSummariesAsync(academicYearId, ct).ConfigureAwait(false);
        IList<ClassFeeSummaryDto> dtos = rows
            .Select(r => new ClassFeeSummaryDto(r.ClassId, r.ClassName, r.StudentCount, r.TotalAmount))
            .ToList();
        return Result<IList<ClassFeeSummaryDto>>.Success(dtos);
    }

    public async Task<Result<ClassFeeAmountsResponseDto>> GetClassAmountsAsync(Guid classId, Guid academicYearId, CancellationToken ct = default)
    {
        if (classId == Guid.Empty || academicYearId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class and academic year are required.");
        }

        IList<ClassFeeSummaryRow> summaries = await _repo.GetClassSummariesAsync(academicYearId, ct).ConfigureAwait(false);
        ClassFeeSummaryRow? summary = summaries.FirstOrDefault(s => s.ClassId == classId);
        IList<ClassFeeAmountRow> rows = await _repo.GetAmountsByClassAsync(classId, academicYearId, ct).ConfigureAwait(false);

        IList<ClassFeeAmountItemDto> items = rows
            .Select(r => new ClassFeeAmountItemDto(
                r.FeeTypeId,
                r.FeeTypeName,
                FeeLabelHelper.CategoryLabel((FeeCategory)r.Category),
                FeeLabelHelper.FrequencyLabel((FeeFrequency)r.Frequency),
                r.Amount))
            .ToList();

        decimal total = items.Sum(i => i.Amount);
        return Result<ClassFeeAmountsResponseDto>.Success(new ClassFeeAmountsResponseDto(
            classId,
            summary?.ClassName ?? "Class",
            academicYearId,
            total,
            items));
    }

    public async Task<Result<ClassFeeAmountsResponseDto>> SaveClassAmountsAsync(
        Guid classId,
        SaveClassFeeAmountsRequestDto request,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || request.AcademicYearId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class and academic year are required.");
        }

        IList<(Guid FeeTypeId, decimal Amount)> amounts = request.Amounts
            .Select(a => (a.FeeTypeId, a.Amount))
            .ToList();

        await _repo.UpsertAmountsAsync(classId, request.AcademicYearId, amounts, ct).ConfigureAwait(false);
        return await GetClassAmountsAsync(classId, request.AcademicYearId, ct).ConfigureAwait(false);
    }
}
