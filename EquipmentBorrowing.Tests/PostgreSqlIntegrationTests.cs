using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_TEST_CONNECTION_STRING")))
        {
            Skip = "Set EQUIPMENT_BORROWING_TEST_CONNECTION_STRING to run PostgreSQL integration tests.";
        }
    }
}

public sealed class PostgreSqlIntegrationTests
{
    [PostgreSqlFact]
    public async Task PostgreSQL_migration_constraints_checkout_return_and_history_persist()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var factory = database.CreateFactory();
        await using (var context = await factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            context.Students.Add(new Student("S-100", "Ada Borrower", "ada@example.edu"));
            context.Equipment.AddRange(
                new Equipment("EQ-100", "Camera"),
                new Equipment("EQ-101", "Microphone"));
            await context.SaveChangesAsync();
        }

        var students = new EfStudentRepository(factory);
        var equipment = new EfEquipmentRepository(factory);
        var borrowings = new EfBorrowingRepository(factory);
        var student = (await students.GetAllAsync()).Single();
        var items = (await equipment.GetAllAsync()).OrderBy(item => item.Id).ToArray();
        var checkout = new BorrowEquipmentService(students, equipment, borrowings);
        var ids = await checkout.BorrowMultipleEquipmentAsync(student.Id, items.Select(item => item.Id).ToArray(), FutureDate());

        Assert.Collection(ids, _ => { }, _ => { });
        Assert.Equal(2, (await borrowings.GetActiveSummariesAsync()).Count);
        await using (var reopened = await factory.CreateDbContextAsync())
        {
            var records = await reopened.Borrowings.AsNoTracking().Where(item => ids.Contains(item.Id)).ToListAsync();
            Assert.Equal(2, records.Count);
            Assert.Single(records.Select(item => item.CheckoutBatchId).Distinct());
            Assert.All(await reopened.Equipment.AsNoTracking().ToListAsync(), item => Assert.False(item.IsAvailable));

            // Both normalized identifiers are protected by database-level unique indexes.
            reopened.Students.Add(new Student(" s-100 ", "Duplicate student"));
            await Assert.ThrowsAsync<DbUpdateException>(() => reopened.SaveChangesAsync());
            reopened.ChangeTracker.Clear();
            reopened.Equipment.Add(new Equipment(" eq-100 ", "Duplicate equipment"));
            await Assert.ThrowsAsync<DbUpdateException>(() => reopened.SaveChangesAsync());
            reopened.ChangeTracker.Clear();

            // The filtered unique index allows history while blocking a second active checkout.
            reopened.Borrowings.Add(new Borrowing(student.Id, items[0].Id, DateTime.Today, FutureDate()));
            await Assert.ThrowsAsync<DbUpdateException>(() => reopened.SaveChangesAsync());
            reopened.ChangeTracker.Clear();

            var dateConstraintItem = new Equipment("EQ-102", "Date constraint item");
            reopened.Equipment.Add(dateConstraintItem);
            await reopened.SaveChangesAsync();
            var checkoutDate = DateTime.Today;
            reopened.Borrowings.Add(new Borrowing(student.Id, dateConstraintItem.Id, checkoutDate, checkoutDate));
            await Assert.ThrowsAsync<DbUpdateException>(() => reopened.SaveChangesAsync());
            reopened.ChangeTracker.Clear();
            reopened.Borrowings.Add(new Borrowing(999999, dateConstraintItem.Id, checkoutDate, FutureDate()));
            await Assert.ThrowsAsync<DbUpdateException>(() => reopened.SaveChangesAsync());
            reopened.ChangeTracker.Clear();
        }

        await new ReturnEquipmentService(borrowings).ReturnEquipmentAsync(ids[0]);
        await using var finalContext = await factory.CreateDbContextAsync();
        var returned = await finalContext.Borrowings.AsNoTracking().SingleAsync(item => item.Id == ids[0]);
        Assert.Equal(BorrowingStatus.Returned, returned.Status);
        Assert.NotNull(returned.ActualReturnDate);
        Assert.DoesNotContain(await borrowings.GetActiveSummariesAsync(), item => item.BorrowingId == ids[0]);
        Assert.Collection(await borrowings.GetStudentHistoryAsync(student.Id), _ => { }, _ => { });
    }

    [PostgreSqlFact]
    public async Task PostgreSQL_multi_checkout_rolls_back_all_writes_after_trigger_failure()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var factory = database.CreateFactory();
        int studentId;
        int firstEquipmentId;
        int secondEquipmentId;
        await using (var context = await factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            var student = new Student("S-200", "Rollback Student");
            var first = new Equipment("EQ-200", "First item");
            var second = new Equipment("EQ-201", "Second item");
            context.AddRange(student, first, second);
            await context.SaveChangesAsync();
            studentId = student.Id;
            firstEquipmentId = first.Id;
            secondEquipmentId = second.Id;

            await context.Database.ExecuteSqlRawAsync("CREATE SEQUENCE \"WriteAttempts\";");
            await context.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION "CountEquipmentWrites"() RETURNS trigger AS $$
                DECLARE attempt bigint;
                BEGIN
                    attempt := nextval('"WriteAttempts"');
                    IF attempt >= 2 THEN
                        RAISE EXCEPTION 'injected failure after the first equipment write';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER "CountEquipmentWritesTrigger"
                BEFORE UPDATE ON "Equipment"
                FOR EACH ROW EXECUTE FUNCTION "CountEquipmentWrites"();
                """);
        }

        var students = new EfStudentRepository(factory);
        var equipment = new EfEquipmentRepository(factory);
        var borrowings = new EfBorrowingRepository(factory);
        var service = new BorrowEquipmentService(students, equipment, borrowings);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.BorrowMultipleEquipmentAsync(
            studentId,
            [firstEquipmentId, secondEquipmentId],
            FutureDate()));

        await using (var verification = await factory.CreateDbContextAsync())
        {
            Assert.Empty(await verification.Borrowings.AsNoTracking().ToListAsync());
            Assert.All(await verification.Equipment.AsNoTracking().ToListAsync(), item => Assert.True(item.IsAvailable));
        }

        await using var connection = new NpgsqlConnection(database.SchemaConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT last_value FROM \"WriteAttempts\"", connection);
        Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) >= 2);
    }

    [PostgreSqlFact]
    public async Task PostgreSQL_concurrent_checkouts_serialize_equipment_and_student_limit_checks()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var factory = database.CreateFactory();
        int firstStudentId;
        int secondStudentId;
        int limitStudentId;
        int contestedEquipmentId;
        int firstLimitEquipmentId;
        int secondLimitEquipmentId;
        int firstAvailableEquipmentId;
        int secondAvailableEquipmentId;
        await using (var context = await factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            var firstStudent = new Student("S-301", "First Student");
            var secondStudent = new Student("S-302", "Second Student");
            var limitStudent = new Student("S-303", "Limit Student");
            var equipment = Enumerable.Range(301, 5)
                .Select(index => new Equipment($"EQ-{index}", $"Concurrent item {index}"))
                .ToArray();
            context.AddRange(firstStudent, secondStudent, limitStudent);
            context.Equipment.AddRange(equipment);
            await context.SaveChangesAsync();

            firstStudentId = firstStudent.Id;
            secondStudentId = secondStudent.Id;
            limitStudentId = limitStudent.Id;
            contestedEquipmentId = equipment[0].Id;
            firstLimitEquipmentId = equipment[1].Id;
            secondLimitEquipmentId = equipment[2].Id;
            firstAvailableEquipmentId = equipment[3].Id;
            secondAvailableEquipmentId = equipment[4].Id;

            equipment[1].MarkAsBorrowed();
            equipment[2].MarkAsBorrowed();
            context.Borrowings.AddRange(
                new Borrowing(limitStudentId, firstLimitEquipmentId, DateTime.Today, FutureDate()),
                new Borrowing(limitStudentId, secondLimitEquipmentId, DateTime.Today, FutureDate()));
            await context.SaveChangesAsync();
        }

        var students = new EfStudentRepository(factory);
        var equipmentRepository = new EfEquipmentRepository(factory);
        var borrowings = new EfBorrowingRepository(factory);
        var checkout = new BorrowEquipmentService(students, equipmentRepository, borrowings);

        var sameEquipmentResults = await Task.WhenAll(
            TryCheckoutAsync(checkout, firstStudentId, contestedEquipmentId),
            TryCheckoutAsync(checkout, secondStudentId, contestedEquipmentId));
        var limitResults = await Task.WhenAll(
            TryCheckoutAsync(checkout, limitStudentId, firstAvailableEquipmentId),
            TryCheckoutAsync(checkout, limitStudentId, secondAvailableEquipmentId));

        Assert.Single(sameEquipmentResults, succeeded => succeeded);
        Assert.Single(sameEquipmentResults, succeeded => !succeeded);
        Assert.Single(limitResults, succeeded => succeeded);
        Assert.Single(limitResults, succeeded => !succeeded);
        Assert.Equal(3, await borrowings.CountActiveByStudentIdAsync(limitStudentId));

        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(1, await verification.Borrowings.AsNoTracking()
            .CountAsync(item => item.EquipmentId == contestedEquipmentId && item.Status == BorrowingStatus.Active));
        Assert.Equal(4, await verification.Borrowings.AsNoTracking()
            .CountAsync(item => item.Status == BorrowingStatus.Active));
    }

    [PostgreSqlFact]
    public async Task PostgreSQL_management_restriction_history_and_search_workflows_persist()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var factory = database.CreateFactory();
        await using (var context = await factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
        }

        var students = new EfStudentRepository(factory);
        var equipment = new EfEquipmentRepository(factory);
        var borrowings = new EfBorrowingRepository(factory);
        var studentManagement = new ManageStudentsService(students);
        var equipmentManagement = new ManageEquipmentService(equipment);
        var checkout = new BorrowEquipmentService(students, equipment, borrowings);
        var studentId = await studentManagement.RegisterAsync("S-600", "Staff Member", "staff@example.edu");
        var firstEquipmentId = await equipmentManagement.RegisterAsync("EQ-601", "Camera Kit", "First description");
        var secondEquipmentId = await equipmentManagement.RegisterAsync("EQ-602", "Audio Kit", "Second description");

        await Assert.ThrowsAsync<InvalidOperationException>(() => studentManagement.RegisterAsync(
            " s-600 ", "Duplicate", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => equipmentManagement.RegisterAsync(
            " eq-601 ", "Duplicate", null));
        Assert.True((await equipment.GetByIdAsync(firstEquipmentId))!.IsAvailable);

        await checkout.BorrowEquipmentAsync(studentId, firstEquipmentId, FutureDate());
        var firstBorrowingId = Assert.Single(await borrowings.GetStudentHistoryAsync(studentId)).BorrowingId;
        await studentManagement.SetEligibilityAsync(studentId, false);
        await studentManagement.SetEligibilityAsync(studentId, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => checkout.BorrowEquipmentAsync(
            studentId, secondEquipmentId, FutureDate().AddDays(10)));
        await new ReturnEquipmentService(borrowings).ReturnEquipmentAsync(firstBorrowingId);
        await studentManagement.UpdateAsync(studentId, "S-600", "Updated Staff Member", null);
        await studentManagement.SetEligibilityAsync(studentId, true);
        await studentManagement.SetEligibilityAsync(studentId, true);
        await checkout.BorrowEquipmentAsync(studentId, secondEquipmentId, FutureDate());
        var secondBorrowingId = (await borrowings.GetStudentHistoryAsync(studentId))
            .Single(item => item.EquipmentId == secondEquipmentId).BorrowingId;
        await equipmentManagement.UpdateAsync(firstEquipmentId, "EQ-601", "Camera Kit Updated", "   ");
        await equipmentManagement.UpdateAsync(secondEquipmentId, "EQ-602", "Audio Kit Updated", "Updated description");

        var allHistory = await borrowings.SearchAsync(new BorrowingSearchCriteria(
            "s-600", BorrowingStatusFilter.All, BorrowingSortField.ExpectedReturnDate, Descending: false));
        Assert.Equal(2, allHistory.Count);
        Assert.Equal(
            new[] { firstBorrowingId, secondBorrowingId }.Order(),
            allHistory.Select(item => item.BorrowingId));
        var firstHistoryRecord = Assert.Single(allHistory, item => item.BorrowingId == firstBorrowingId);
        Assert.Equal(BorrowingStatus.Returned, firstHistoryRecord.Status);
        Assert.NotNull(firstHistoryRecord.ActualReturnDate);
        Assert.Equal("Updated Staff Member", firstHistoryRecord.StudentName);
        Assert.Null((await equipment.GetByIdAsync(firstEquipmentId))!.Description);
        Assert.Null((await students.GetByIdAsync(studentId))!.ContactInformation);
        Assert.True((await equipment.GetByIdAsync(firstEquipmentId))!.IsAvailable);
        Assert.False((await equipment.GetByIdAsync(secondEquipmentId))!.IsAvailable);
        Assert.Single(await borrowings.GetEquipmentHistoryAsync(firstEquipmentId));
        Assert.Equal(2, (await borrowings.SearchAsync(new BorrowingSearchCriteria("UPDATED STAFF", BorrowingStatusFilter.All))).Count);
        Assert.Single(await borrowings.SearchAsync(new BorrowingSearchCriteria("camera kit", BorrowingStatusFilter.Returned)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReturnEquipmentService(borrowings)
            .ReturnEquipmentAsync(firstBorrowingId));

        await using var reopened = await factory.CreateDbContextAsync();
        Assert.Equal(2, await reopened.Borrowings.AsNoTracking().CountAsync());
        Assert.Equal(1, await reopened.Borrowings.AsNoTracking()
            .CountAsync(item => item.Status == BorrowingStatus.Active && item.EquipmentId == secondEquipmentId));
    }

    private static async Task<bool> TryCheckoutAsync(BorrowEquipmentService service, int studentId, int equipmentId)
    {
        try
        {
            await service.BorrowEquipmentAsync(studentId, equipmentId, FutureDate());
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static DateTime FutureDate() => DateTime.Today.AddDays(10);

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        public string SchemaConnectionString { get; }

        private PostgreSqlTestDatabase(string adminConnectionString, string schemaConnectionString)
        {
            _adminConnectionString = adminConnectionString;
            SchemaConnectionString = schemaConnectionString;
        }

        public static async Task<PostgreSqlTestDatabase> CreateAsync()
        {
            var baseConnectionString = Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_TEST_CONNECTION_STRING")!;
            var schema = $"equipment_borrowing_test_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(baseConnectionString);
            await connection.OpenAsync();
            await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            var schemaConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            return new PostgreSqlTestDatabase(baseConnectionString, schemaConnectionString);
        }

        public IDbContextFactory<EquipmentBorrowingDbContext> CreateFactory() =>
            new TestDbContextFactory(new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
                .UseNpgsql(SchemaConnectionString)
                .Options);

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            var schema = new NpgsqlConnectionStringBuilder(SchemaConnectionString).SearchPath;
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<EquipmentBorrowingDbContext> options)
        : IDbContextFactory<EquipmentBorrowingDbContext>
    {
        public EquipmentBorrowingDbContext CreateDbContext() => new(options);

        public Task<EquipmentBorrowingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
