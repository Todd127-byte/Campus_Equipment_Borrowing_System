using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public sealed class EfStudentRepository(IDbContextFactory<EquipmentBorrowingDbContext> contextFactory)
    : IStudentRepository
{
    public async Task<IEnumerable<Student>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Students.AsNoTracking().OrderBy(student => student.Name).ToListAsync(cancellationToken);
    }

    public async Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Students.AsNoTracking().FirstOrDefaultAsync(student => student.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByNormalizedNumberAsync(
        string normalizedNumber,
        int? exceptStudentId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Students.AsNoTracking().Where(student => student.NormalizedStudentNumber == normalizedNumber);
        if (exceptStudentId.HasValue)
        {
            query = query.Where(student => student.Id != exceptStudentId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Students.Add(student);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("That institutional student number is already registered.", exception);
        }
    }

    public async Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Attach(student);
        var entry = context.Entry(student);
        entry.Property(item => item.InstitutionalStudentNumber).IsModified = true;
        entry.Property(item => item.NormalizedStudentNumber).IsModified = true;
        entry.Property(item => item.Name).IsModified = true;
        entry.Property(item => item.ContactInformation).IsModified = true;
        entry.Property(item => item.IsAllowedToBorrow).IsModified = true;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("That institutional student number belongs to another student.", exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
