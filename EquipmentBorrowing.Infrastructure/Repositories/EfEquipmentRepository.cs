using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public sealed class EfEquipmentRepository(IDbContextFactory<EquipmentBorrowingDbContext> contextFactory)
    : IEquipmentRepository
{
    public async Task<IEnumerable<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Equipment.AsNoTracking().OrderBy(item => item.EquipmentIdentifier).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Equipment.AsNoTracking()
            .Where(item => item.IsAvailable)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Equipment.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByNormalizedIdentifierAsync(
        string normalizedIdentifier,
        int? exceptEquipmentId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Equipment.AsNoTracking().Where(item => item.NormalizedIdentifier == normalizedIdentifier);
        if (exceptEquipmentId.HasValue)
        {
            query = query.Where(item => item.Id != exceptEquipmentId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Equipment.Add(equipment);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("That equipment identifier is already registered.", exception);
        }
    }

    public async Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Attach(equipment);
        var entry = context.Entry(equipment);
        entry.Property(item => item.EquipmentIdentifier).IsModified = true;
        entry.Property(item => item.NormalizedIdentifier).IsModified = true;
        entry.Property(item => item.Name).IsModified = true;
        entry.Property(item => item.Description).IsModified = true;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("That equipment identifier belongs to another item.", exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
