using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public sealed class InMemoryBorrowingRepository : IBorrowingRepository
{
    private readonly List<Borrowing> _borrowings = [];
    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public InMemoryBorrowingRepository(IStudentRepository? studentRepository = null, IEquipmentRepository? equipmentRepository = null)
    {
        _studentRepository = studentRepository ?? new InMemoryStudentRepository();
        _equipmentRepository = equipmentRepository ?? new InMemoryEquipmentRepository();
    }

    public async Task<IReadOnlyList<ActiveBorrowingSummary>> GetActiveSummariesAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<ActiveBorrowingSummary>();
        foreach (var borrowing in _borrowings.Where(item => item.Status == BorrowingStatus.Active))
        {
            var student = await _studentRepository.GetByIdAsync(borrowing.StudentId, cancellationToken);
            var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);
            if (student is not null && equipment is not null)
            {
                summaries.Add(new ActiveBorrowingSummary(
                    borrowing.Id,
                    student.Id,
                    student.Name,
                    equipment.Id,
                    equipment.Name,
                    borrowing.ExpectedReturnDate,
                    borrowing.Status,
                    borrowing.DateBorrowed,
                    borrowing.ActualReturnDate,
                    student.InstitutionalStudentNumber,
                    equipment.EquipmentIdentifier));
            }
        }

        return summaries.OrderBy(item => item.ExpectedReturnDate).ThenBy(item => item.BorrowingId).ToArray();
    }

    public Task<IEnumerable<Borrowing>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Borrowing>>(_borrowings.OrderByDescending(item => item.DateBorrowed).ThenBy(item => item.Id).ToArray());

    public Task<Borrowing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_borrowings.FirstOrDefault(item => item.Id == id));

    public async Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            _borrowings.Add(borrowing);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<int> CountActiveByStudentIdAsync(int studentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_borrowings.Count(item => item.StudentId == studentId && item.Status == BorrowingStatus.Active));

    public async Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var index = _borrowings.FindIndex(existing => existing.Id == borrowing.Id);
            if (index < 0)
            {
                throw new InvalidOperationException("Borrowing record was not found.");
            }

            _borrowings[index] = borrowing;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<Guid>> CreateCheckoutAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (equipmentIds.Count == 0 || equipmentIds.Distinct().Count() != equipmentIds.Count)
            {
                throw new InvalidOperationException("Select one or more distinct equipment items.");
            }

            var student = await _studentRepository.GetByIdAsync(studentId, cancellationToken)
                ?? throw new InvalidOperationException("Student does not exist.");
            if (!student.IsAllowedToBorrow)
            {
                throw new InvalidOperationException("Student is not allowed to borrow equipment.");
            }

            if (_borrowings.Count(item => item.StudentId == studentId && item.Status == BorrowingStatus.Active) + equipmentIds.Count >
                BorrowingRules.MaximumActiveBorrowings)
            {
                throw new InvalidOperationException("The student has reached the maximum of three active borrowings.");
            }

            var equipmentItems = new List<Equipment>();
            foreach (var equipmentId in equipmentIds.Order())
            {
                var equipment = await _equipmentRepository.GetByIdAsync(equipmentId, cancellationToken)
                    ?? throw new InvalidOperationException($"Equipment #{equipmentId} does not exist.");
                if (!equipment.IsAvailable)
                {
                    throw new InvalidOperationException($"{equipment.Name} is no longer available.");
                }

                equipmentItems.Add(equipment);
            }

            var batchId = Guid.NewGuid();
            var checkoutTime = BorrowingRules.LocalToday;
            var expected = BorrowingRules.NormalizeLocalCalendarDate(expectedReturnDate);
            if (expected <= checkoutTime)
            {
                throw new InvalidOperationException("Expected return date must be later than checkout.");
            }

            var records = equipmentItems.Select(item => new Borrowing(
                studentId,
                item.Id,
                checkoutTime,
                expected,
                checkoutBatchId: batchId)).ToArray();
            foreach (var equipment in equipmentItems)
            {
                equipment.MarkAsBorrowed();
                await _equipmentRepository.UpdateAsync(equipment, cancellationToken);
            }

            _borrowings.AddRange(records);
            return records.Select(item => item.Id).ToArray();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task ReturnAsync(Guid borrowingId, DateTime actualReturnDate, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var borrowing = _borrowings.FirstOrDefault(item => item.Id == borrowingId)
                ?? throw new InvalidOperationException("Borrowing record was not found.");
            if (borrowing.Status != BorrowingStatus.Active)
            {
                throw new InvalidOperationException("Only an active borrowing can be returned.");
            }

            var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken)
                ?? throw new InvalidOperationException("The equipment record no longer exists.");
            borrowing.MarkAsReturned(actualReturnDate);
            equipment.MarkAsAvailable();
            await _equipmentRepository.UpdateAsync(equipment, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<IReadOnlyList<BorrowingRecordSummary>> GetStudentHistoryAsync(int studentId, CancellationToken cancellationToken = default) =>
        GetRecordsAsync(new BorrowingSearchCriteria(Descending: true), studentId, null, cancellationToken);

    public Task<IReadOnlyList<BorrowingRecordSummary>> GetEquipmentHistoryAsync(int equipmentId, CancellationToken cancellationToken = default) =>
        GetRecordsAsync(new BorrowingSearchCriteria(Descending: true), null, equipmentId, cancellationToken);

    public Task<IReadOnlyList<BorrowingRecordSummary>> SearchAsync(BorrowingSearchCriteria criteria, CancellationToken cancellationToken = default) =>
        GetRecordsAsync(criteria, null, null, cancellationToken);

    private async Task<IReadOnlyList<BorrowingRecordSummary>> GetRecordsAsync(
        BorrowingSearchCriteria criteria,
        int? studentId,
        int? equipmentId,
        CancellationToken cancellationToken)
    {
        var results = new List<BorrowingRecordSummary>();
        foreach (var borrowing in _borrowings)
        {
            if (studentId.HasValue && borrowing.StudentId != studentId.Value ||
                equipmentId.HasValue && borrowing.EquipmentId != equipmentId.Value ||
                criteria.Status == BorrowingStatusFilter.Active && borrowing.Status != BorrowingStatus.Active ||
                criteria.Status == BorrowingStatusFilter.Returned && borrowing.Status != BorrowingStatus.Returned)
            {
                continue;
            }

            var student = await _studentRepository.GetByIdAsync(borrowing.StudentId, cancellationToken);
            var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);
            if (student is null || equipment is null)
            {
                continue;
            }

            var record = new BorrowingRecordSummary(
                borrowing.Id,
                student.Id,
                student.InstitutionalStudentNumber,
                student.Name,
                equipment.Id,
                equipment.EquipmentIdentifier,
                equipment.Name,
                borrowing.DateBorrowed,
                borrowing.ExpectedReturnDate,
                borrowing.ActualReturnDate,
                borrowing.Status);
            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                var term = criteria.SearchText.Trim();
                if (!record.StudentName.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                    !record.StudentNumber.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                    !record.EquipmentName.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            results.Add(record);
        }

        var ordered = criteria.SortField == BorrowingSortField.ExpectedReturnDate
            ? criteria.Descending ? results.OrderByDescending(item => item.ExpectedReturnDate) : results.OrderBy(item => item.ExpectedReturnDate)
            : criteria.Descending ? results.OrderByDescending(item => item.DateBorrowed) : results.OrderBy(item => item.DateBorrowed);
        return ordered.ThenBy(item => item.BorrowingId).ToArray();
    }
}
