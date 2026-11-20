using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IBorrowingRepository
{
    Task<IReadOnlyList<ActiveBorrowingSummary>> GetActiveSummariesAsync(
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Borrowing>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Borrowing?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByStudentIdAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> CreateCheckoutAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default);

    Task ReturnAsync(
        Guid borrowingId,
        DateTime actualReturnDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BorrowingRecordSummary>> GetStudentHistoryAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BorrowingRecordSummary>> GetEquipmentHistoryAsync(
        int equipmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BorrowingRecordSummary>> SearchAsync(
        BorrowingSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
