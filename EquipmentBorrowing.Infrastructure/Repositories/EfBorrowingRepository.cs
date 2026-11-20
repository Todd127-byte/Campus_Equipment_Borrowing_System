using System.Data;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public sealed class EfBorrowingRepository(IDbContextFactory<EquipmentBorrowingDbContext> contextFactory)
    : IBorrowingRepository
{
    public async Task<IReadOnlyList<ActiveBorrowingSummary>> GetActiveSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
            from borrowing in context.Borrowings.AsNoTracking()
            join student in context.Students.AsNoTracking() on borrowing.StudentId equals student.Id
            join equipment in context.Equipment.AsNoTracking() on borrowing.EquipmentId equals equipment.Id
            where borrowing.Status == BorrowingStatus.Active
            orderby borrowing.ExpectedReturnDate, borrowing.Id
            select new ActiveBorrowingSummary(
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
                equipment.EquipmentIdentifier))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Borrowing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Borrowings.AsNoTracking()
            .OrderByDescending(item => item.DateBorrowed)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Borrowing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Borrowings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Borrowings.Add(borrowing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountActiveByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Borrowings.AsNoTracking().CountAsync(
            item => item.StudentId == studentId && item.Status == BorrowingStatus.Active,
            cancellationToken);
    }

    public async Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Attach(borrowing);
        context.Entry(borrowing).Property(item => item.Status).IsModified = true;
        context.Entry(borrowing).Property(item => item.ActualReturnDate).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> CreateCheckoutAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        if (equipmentIds.Count == 0 || equipmentIds.Distinct().Count() != equipmentIds.Count)
        {
            throw new InvalidOperationException("Select one or more distinct equipment items.");
        }

        var equipmentIdsInLockOrder = equipmentIds.Order().ToArray();
        var batchId = Guid.NewGuid();
        var checkoutDate = BorrowingRules.LocalToday;
        var expectedDateUtc = BorrowingRules.NormalizeLocalCalendarDate(expectedReturnDate);
        if (expectedDateUtc <= checkoutDate)
        {
            throw new InvalidOperationException("Expected return date must be later than checkout.");
        }

        var borrowings = equipmentIds.Select(equipmentId =>
            new Borrowing(studentId, equipmentId, checkoutDate, expectedDateUtc, checkoutBatchId: batchId)).ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var student = await LockStudentAsync(context, studentId, cancellationToken)
                ?? throw new InvalidOperationException("Student does not exist.");

            if (!student.IsAllowedToBorrow)
            {
                throw new InvalidOperationException("Student is not allowed to borrow equipment.");
            }

            var activeCount = await context.Borrowings.CountAsync(
                borrowing => borrowing.StudentId == studentId && borrowing.Status == BorrowingStatus.Active,
                cancellationToken);
            if (activeCount + equipmentIds.Count > BorrowingRules.MaximumActiveBorrowings)
            {
                throw new InvalidOperationException("The student has reached the maximum of three active borrowings.");
            }

            var lockedEquipment = new List<Equipment>(equipmentIds.Count);
            foreach (var equipmentId in equipmentIdsInLockOrder)
            {
                var equipment = await LockEquipmentAsync(context, equipmentId, cancellationToken)
                    ?? throw new InvalidOperationException($"Equipment #{equipmentId} does not exist.");
                if (!equipment.IsAvailable)
                {
                    throw new InvalidOperationException($"{equipment.Name} is no longer available.");
                }

                lockedEquipment.Add(equipment);
            }

            foreach (var equipment in lockedEquipment)
            {
                equipment.MarkAsBorrowed();
            }

            context.Borrowings.AddRange(borrowings);
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await transaction.CommitAsync(cancellationToken);
                return borrowings.Select(item => item.Id).ToArray();
            }
            catch (Exception commitException)
            {
                try
                {
                    await transaction.DisposeAsync();
                }
                catch
                {
                    // Inspect the operation using a fresh context even if rollback/disposal lost its connection.
                }

                var outcome = await InspectCheckoutOutcomeAsync(batchId, borrowings, commitException);
                if (outcome == CheckoutOutcome.Committed)
                {
                    return borrowings.Select(item => item.Id).ToArray();
                }

                throw new OperationNotCommittedException(
                    "The checkout was not committed. Refresh the lists before submitting another request.",
                    commitException);
            }
        }
        catch (DbUpdateException exception) when (GetPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await TryRollbackAsync(transaction, cancellationToken);
            throw new InvalidOperationException("One or more selected items were borrowed by another request. Refresh the available equipment list.", exception);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await TryRollbackAsync(transaction, cancellationToken);
            throw new InvalidOperationException("A concurrent database update prevented checkout. Refresh and review the current records.", exception);
        }
        catch
        {
            await TryRollbackAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task ReturnAsync(Guid borrowingId, DateTime actualReturnDate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var borrowing = await LockBorrowingAsync(context, borrowingId, cancellationToken)
                ?? throw new InvalidOperationException("Borrowing record was not found. Refresh and select an active record.");
            if (borrowing.Status != BorrowingStatus.Active)
            {
                throw new InvalidOperationException("Only an active borrowing can be returned.");
            }

            var equipment = await LockEquipmentAsync(context, borrowing.EquipmentId, cancellationToken)
                ?? throw new InvalidOperationException("The equipment record no longer exists.");

            borrowing.MarkAsReturned(actualReturnDate);
            equipment.MarkAsAvailable();
            await context.SaveChangesAsync(cancellationToken);
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception commitException)
            {
                try
                {
                    await transaction.DisposeAsync();
                }
                catch
                {
                    // Continue with a fresh-context outcome check.
                }
                try
                {
                    await using var verification = await contextFactory.CreateDbContextAsync(cancellationToken);
                    var returned = await verification.Borrowings.AsNoTracking()
                        .AnyAsync(item => item.Id == borrowingId && item.Status == BorrowingStatus.Returned && item.ActualReturnDate != null, cancellationToken);
                    if (returned)
                    {
                        return;
                    }

                    var stillActive = await verification.Borrowings.AsNoTracking()
                        .AnyAsync(item => item.Id == borrowingId && item.Status == BorrowingStatus.Active, cancellationToken);
                    if (stillActive)
                    {
                        throw new OperationNotCommittedException("The return was not committed. Refresh the lists before submitting another request.", commitException);
                    }
                }
                catch (OperationNotCommittedException)
                {
                    throw;
                }
                catch (Exception inspectionException)
                {
                    throw new OperationOutcomeUnknownException(borrowingId, "return", new AggregateException(commitException, inspectionException));
                }

                throw new OperationOutcomeUnknownException(borrowingId, "return", commitException);
            }
        }
        catch
        {
            await TryRollbackAsync(transaction, cancellationToken);
            throw;
        }
    }

    public Task<IReadOnlyList<BorrowingRecordSummary>> GetStudentHistoryAsync(int studentId, CancellationToken cancellationToken = default) =>
        GetRecordsAsync(null, studentId, null, cancellationToken);

    public Task<IReadOnlyList<BorrowingRecordSummary>> GetEquipmentHistoryAsync(int equipmentId, CancellationToken cancellationToken = default) =>
        GetRecordsAsync(null, null, equipmentId, cancellationToken);

    public Task<IReadOnlyList<BorrowingRecordSummary>> SearchAsync(BorrowingSearchCriteria criteria, CancellationToken cancellationToken = default) =>
        GetRecordsAsync(criteria, null, null, cancellationToken);

    private async Task<IReadOnlyList<BorrowingRecordSummary>> GetRecordsAsync(
        BorrowingSearchCriteria? criteria,
        int? studentId,
        int? equipmentId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query =
            from borrowing in context.Borrowings.AsNoTracking()
            join student in context.Students.AsNoTracking() on borrowing.StudentId equals student.Id
            join equipment in context.Equipment.AsNoTracking() on borrowing.EquipmentId equals equipment.Id
            select new { borrowing, student, equipment };

        if (studentId.HasValue)
        {
            query = query.Where(row => row.borrowing.StudentId == studentId.Value);
        }

        if (equipmentId.HasValue)
        {
            query = query.Where(row => row.borrowing.EquipmentId == equipmentId.Value);
        }

        if (criteria is not null)
        {
            if (criteria.Status == BorrowingStatusFilter.Active)
            {
                query = query.Where(row => row.borrowing.Status == BorrowingStatus.Active);
            }
            else if (criteria.Status == BorrowingStatusFilter.Returned)
            {
                query = query.Where(row => row.borrowing.Status == BorrowingStatus.Returned);
            }

            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                var term = criteria.SearchText.Trim().ToLowerInvariant();
                query = query.Where(row => row.student.Name.ToLower().Contains(term) ||
                                           row.student.InstitutionalStudentNumber.ToLower().Contains(term) ||
                                           row.equipment.Name.ToLower().Contains(term));
            }
        }

        if (criteria is not null)
        {
            var orderedQuery = criteria.SortField == BorrowingSortField.ExpectedReturnDate
                ? criteria.Descending
                    ? query.OrderByDescending(row => row.borrowing.ExpectedReturnDate)
                    : query.OrderBy(row => row.borrowing.ExpectedReturnDate)
                : criteria.Descending
                    ? query.OrderByDescending(row => row.borrowing.DateBorrowed)
                    : query.OrderBy(row => row.borrowing.DateBorrowed);

            query = orderedQuery.ThenBy(row => row.borrowing.Id);
        }
        else
        {
            query = query.OrderByDescending(row => row.borrowing.DateBorrowed)
                .ThenBy(row => row.borrowing.Id);
        }

        return await query.Select(row => new BorrowingRecordSummary(
                row.borrowing.Id,
                row.student.Id,
                row.student.InstitutionalStudentNumber,
                row.student.Name,
                row.equipment.Id,
                row.equipment.EquipmentIdentifier,
                row.equipment.Name,
                row.borrowing.DateBorrowed,
                row.borrowing.ExpectedReturnDate,
                row.borrowing.ActualReturnDate,
                row.borrowing.Status))
            .ToListAsync(cancellationToken);
    }

    private async Task<CheckoutOutcome> InspectCheckoutOutcomeAsync(
        Guid batchId,
        IReadOnlyCollection<Borrowing> requested,
        Exception commitException)
    {
        try
        {
            await using var verification = await contextFactory.CreateDbContextAsync();
            var persisted = await verification.Borrowings.AsNoTracking()
                .Where(item => item.CheckoutBatchId == batchId)
                .Select(item => item.Id)
                .ToListAsync();
            if (persisted.Count == requested.Count && requested.All(item => persisted.Contains(item.Id)))
            {
                return CheckoutOutcome.Committed;
            }

            if (persisted.Count == 0)
            {
                return CheckoutOutcome.NotCommitted;
            }

            throw new OperationOutcomeUnknownException(batchId, "multi-item checkout", commitException);
        }
        catch (OperationOutcomeUnknownException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            throw new OperationOutcomeUnknownException(
                batchId,
                "multi-item checkout",
                new AggregateException(commitException, inspectionException));
        }
    }

    private static PostgresException? GetPostgresException(DbUpdateException exception) =>
        exception.InnerException as PostgresException;

    private static Task<Student?> LockStudentAsync(
        EquipmentBorrowingDbContext context,
        int studentId,
        CancellationToken cancellationToken) =>
        context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? context.Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {studentId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : context.Students.SingleOrDefaultAsync(item => item.Id == studentId, cancellationToken);

    private static Task<Equipment?> LockEquipmentAsync(
        EquipmentBorrowingDbContext context,
        int equipmentId,
        CancellationToken cancellationToken) =>
        context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? context.Equipment.FromSqlInterpolated($"SELECT * FROM \"Equipment\" WHERE \"Id\" = {equipmentId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : context.Equipment.SingleOrDefaultAsync(item => item.Id == equipmentId, cancellationToken);

    private static Task<Borrowing?> LockBorrowingAsync(
        EquipmentBorrowingDbContext context,
        Guid borrowingId,
        CancellationToken cancellationToken) =>
        context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? context.Borrowings.FromSqlInterpolated($"SELECT * FROM \"Borrowings\" WHERE \"Id\" = {borrowingId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : context.Borrowings.SingleOrDefaultAsync(item => item.Id == borrowingId, cancellationToken);

    private static async Task TryRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Transaction disposal also attempts rollback; preserve the original failure.
        }
    }

    private enum CheckoutOutcome
    {
        Committed,
        NotCommitted
    }
}
