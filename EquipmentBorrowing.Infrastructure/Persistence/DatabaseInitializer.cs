using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<EquipmentBorrowingDbContext> _contextFactory;
    private readonly IConfiguration? _configuration;

    public DatabaseInitializer(
        IDbContextFactory<EquipmentBorrowingDbContext> contextFactory,
        IConfiguration? configuration = null)
    {
        _contextFactory = contextFactory;
        _configuration = configuration;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration is not null)
        {
            _ = DatabasePaths.GetConnectionString(_configuration);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite is used only by fast isolated repository tests. The application runs PostgreSQL migrations.
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        // Seed only a completely empty database. Existing user data is never reset or overwritten.
        if (await context.Students.AnyAsync(cancellationToken) ||
            await context.Equipment.AnyAsync(cancellationToken) ||
            await context.Borrowings.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.Students.AddRange(
            new Student(1, "S-0001", "Juan Dela Cruz", "juan@example.edu", true),
            new Student(2, "S-0002", "Maria Santos", null, true),
            new Student(3, "S-0003", "Ted Balaba", null, true));

        context.Equipment.AddRange(
            new Equipment(101, "EQ-101", "Arduino Uno", "Microcontroller starter board"),
            new Equipment(102, "EQ-102", "Raspberry Pi", "Single-board computer", isAvailable: false),
            new Equipment(103, "EQ-103", "USB Webcam", "USB camera"),
            new Equipment(104, "EQ-104", "Portable Projector", "Classroom projector"),
            new Equipment(105, "EQ-105", "Spare Monitor", "External display"));

        context.Borrowings.Add(new Borrowing(
            studentId: 1,
            equipmentId: 102,
            dateBorrowed: BorrowingRules.NormalizeLocalCalendarDate(DateTime.Today.AddDays(-2)),
            expectedReturnDate: BorrowingRules.NormalizeLocalCalendarDate(DateTime.Today.AddDays(12))));

        await context.SaveChangesAsync(cancellationToken);
        if (context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"Students\"', 'Id'), GREATEST((SELECT MAX(\"Id\") FROM \"Students\"), 1));",
                cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"Equipment\"', 'Id'), GREATEST((SELECT MAX(\"Id\") FROM \"Equipment\"), 1));",
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
