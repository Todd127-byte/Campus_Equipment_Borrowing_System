using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

public sealed class BrowseBorrowingsService(IBorrowingRepository borrowingRepository)
{
    public Task<IReadOnlyList<BorrowingRecordSummary>> GetStudentHistoryAsync(
        int studentId,
        CancellationToken cancellationToken = default) =>
        borrowingRepository.GetStudentHistoryAsync(studentId, cancellationToken);

    public Task<IReadOnlyList<BorrowingRecordSummary>> GetEquipmentHistoryAsync(
        int equipmentId,
        CancellationToken cancellationToken = default) =>
        borrowingRepository.GetEquipmentHistoryAsync(equipmentId, cancellationToken);

    public Task<IReadOnlyList<BorrowingRecordSummary>> SearchAsync(
        BorrowingSearchCriteria criteria,
        CancellationToken cancellationToken = default) =>
        borrowingRepository.SearchAsync(criteria, cancellationToken);
}
