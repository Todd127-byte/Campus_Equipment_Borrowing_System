using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Repositories;

namespace EquipmentBorrowing.Tests;

public sealed class BorrowingWorkflowTests
{
    [Fact]
    public async Task Single_checkout_updates_equipment_and_creates_active_record()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        await service.BorrowEquipmentAsync(1, 101, FutureDate());

        Assert.False((await equipment.GetByIdAsync(101))!.IsAvailable);
        Assert.Equal(1, await borrowings.CountActiveByStudentIdAsync(1));
    }

    [Fact]
    public async Task Checkout_rejects_restricted_student_and_preserves_equipment()
    {
        var students = new InMemoryStudentRepository([new Student(2, "S-0002", "Restricted", null, false, 3)]);
        var equipment = new InMemoryEquipmentRepository();
        var borrowings = new InMemoryBorrowingRepository(students, equipment);
        var service = new BorrowEquipmentService(students, equipment, borrowings);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowEquipmentAsync(2, 101, FutureDate()));

        Assert.Contains("not allowed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True((await equipment.GetByIdAsync(101))!.IsAvailable);
    }

    [Fact]
    public async Task Checkout_rejects_unavailable_equipment()
    {
        var (students, equipment, _, service) = CreateWorkflow();
        (await equipment.GetByIdAsync(102))!.MarkAsBorrowed();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowEquipmentAsync(1, 102, FutureDate()));

        Assert.Contains("not available", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checkout_enforces_three_item_student_limit()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        for (var index = 0; index < 3; index++)
        {
            await borrowings.AddAsync(new Borrowing(1, 900 + index, DateTime.Today, FutureDate()));
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowEquipmentAsync(1, 101, FutureDate()));

        Assert.Contains("maximum", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True((await equipment.GetByIdAsync(101))!.IsAvailable);
    }

    [Fact]
    public async Task Checkout_rejects_due_date_that_is_not_after_today()
    {
        var (_, _, _, service) = CreateWorkflow();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowEquipmentAsync(1, 101, DateTime.Today));
    }

    [Fact]
    public async Task Checkout_uses_local_calendar_dates_for_checkout_due_and_return()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        var expectedLocalDate = DateTime.Today.AddDays(1).AddHours(23);

        await service.BorrowEquipmentAsync(1, 101, expectedLocalDate);
        var borrowing = Assert.Single(await borrowings.GetAllAsync());

        Assert.Equal(BorrowingRules.LocalToday, borrowing.DateBorrowed);
        Assert.Equal(BorrowingRules.NormalizeLocalCalendarDate(expectedLocalDate), borrowing.ExpectedReturnDate);

        await new ReturnEquipmentService(borrowings, equipment).ReturnEquipmentAsync(borrowing.Id);

        Assert.Equal(BorrowingRules.LocalToday, borrowing.ActualReturnDate);
    }

    [Fact]
    public async Task Multiple_checkout_is_one_batch_and_returns_all_borrowing_ids()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();

        var ids = await service.BorrowMultipleEquipmentAsync(1, [101, 103], FutureDate());

        Assert.Collection(ids, _ => { }, _ => { });
        Assert.Equal(2, await borrowings.CountActiveByStudentIdAsync(1));
        Assert.False((await equipment.GetByIdAsync(101))!.IsAvailable);
        Assert.False((await equipment.GetByIdAsync(103))!.IsAvailable);
        var records = (await borrowings.GetAllAsync()).Where(item => ids.Contains(item.Id)).ToArray();
        Assert.Single(records.Select(item => item.CheckoutBatchId).Distinct());
    }

    [Fact]
    public async Task Multiple_checkout_rejects_fewer_than_two_or_duplicate_items_without_changes()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101], FutureDate()));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101, 101], FutureDate()));

        Assert.Empty(await borrowings.GetAllAsync());
        Assert.True((await equipment.GetByIdAsync(101))!.IsAvailable);
    }

    [Fact]
    public async Task Multiple_checkout_rejects_missing_unavailable_ineligible_over_limit_and_invalid_requests()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        var future = FutureDate();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101, 105], DateTime.Today));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101, 9999], future));

        (await equipment.GetByIdAsync(103))!.MarkAsBorrowed();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101, 103], future));

        (await equipment.GetByIdAsync(102))!.MarkAsBorrowed();
        (await equipment.GetByIdAsync(104))!.MarkAsBorrowed();
        await borrowings.AddAsync(new Borrowing(1, 102, DateTime.Today.AddDays(-2), future));
        await borrowings.AddAsync(new Borrowing(1, 104, DateTime.Today.AddDays(-1), future));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101, 105], future));

        await new ManageStudentsService(students).SetEligibilityAsync(1, false);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BorrowMultipleEquipmentAsync(1, [101, 105], future));

        Assert.Equal(2, (await borrowings.GetAllAsync()).Count());
        Assert.True((await equipment.GetByIdAsync(101))!.IsAvailable);
        Assert.True((await equipment.GetByIdAsync(105))!.IsAvailable);
    }

    [Fact]
    public async Task Return_records_actual_date_and_keeps_the_history()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        await service.BorrowEquipmentAsync(1, 101, FutureDate());
        var borrowing = Assert.Single(await borrowings.GetAllAsync());
        var returnService = new ReturnEquipmentService(borrowings, equipment);

        await returnService.ReturnEquipmentAsync(borrowing.Id);

        Assert.Equal(BorrowingStatus.Returned, borrowing.Status);
        Assert.NotNull(borrowing.ActualReturnDate);
        Assert.True((await equipment.GetByIdAsync(101))!.IsAvailable);
        Assert.Equal(0, await borrowings.CountActiveByStudentIdAsync(1));
        Assert.Single(await borrowings.GetStudentHistoryAsync(1));
    }

    [Fact]
    public async Task Return_rejects_a_record_that_is_already_returned()
    {
        var equipment = new InMemoryEquipmentRepository();
        var borrowings = new InMemoryBorrowingRepository(equipmentRepository: equipment);
        var borrowing = new Borrowing(1, 101, DateTime.Today.AddDays(-2), FutureDate());
        borrowing.MarkAsReturned();
        await borrowings.AddAsync(borrowing);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ReturnEquipmentService(borrowings).ReturnEquipmentAsync(borrowing.Id));

        Assert.Contains("Only an active", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restricted_student_can_return_an_existing_borrowing()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        await service.BorrowEquipmentAsync(1, 101, FutureDate());
        var borrowing = Assert.Single(await borrowings.GetAllAsync());
        var management = new ManageStudentsService(students);
        await management.SetEligibilityAsync(1, false);

        await new ReturnEquipmentService(borrowings, equipment).ReturnEquipmentAsync(borrowing.Id);

        Assert.False((await students.GetByIdAsync(1))!.IsAllowedToBorrow);
        Assert.Equal(BorrowingStatus.Returned, borrowing.Status);
        Assert.True((await equipment.GetByIdAsync(101))!.IsAvailable);
    }

    [Fact]
    public async Task Student_management_normalizes_duplicates_and_preserves_borrowing_links_on_edit()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        await service.BorrowEquipmentAsync(1, 101, FutureDate());
        var management = new ManageStudentsService(students);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => management.RegisterAsync(" s-0001 ", "Duplicate name", null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => management.UpdateAsync(1, "S-0002", "Duplicate number", null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => management.UpdateAsync(1, "S-0001", "   ", null));
        await management.UpdateAsync(1, "S-1001", "Juan Updated", null);

        Assert.Equal(1, (await borrowings.GetAllAsync()).Single().StudentId);
        Assert.Equal("Juan Updated", (await students.GetByIdAsync(1))!.Name);
        Assert.Null((await students.GetByIdAsync(1))!.ContactInformation);
    }

    [Fact]
    public async Task Student_and_equipment_registration_apply_defaults_and_reject_blank_required_values()
    {
        var students = new InMemoryStudentRepository();
        var equipment = new InMemoryEquipmentRepository();
        var studentManagement = new ManageStudentsService(students);
        var equipmentManagement = new ManageEquipmentService(equipment);

        var studentId = await studentManagement.RegisterAsync(" S-NEW ", "New Student", null);
        var equipmentId = await equipmentManagement.RegisterAsync(" EQ-NEW ", "New Equipment", null);

        var student = await students.GetByIdAsync(studentId);
        var item = await equipment.GetByIdAsync(equipmentId);
        Assert.NotNull(student);
        Assert.True(student.IsAllowedToBorrow);
        Assert.Null(student.ContactInformation);
        Assert.NotNull(item);
        Assert.True(item.IsAvailable);
        Assert.Null(item.Description);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => studentManagement.RegisterAsync(" ", "Name", null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => studentManagement.RegisterAsync("S-WHITESPACE", "Name", "   "));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => equipmentManagement.RegisterAsync("EQ-EMPTY", " ", null));
    }

    [Fact]
    public async Task Student_and_equipment_histories_stay_scoped_and_return_empty_for_no_matches()
    {
        var students = new InMemoryStudentRepository();
        var equipment = new InMemoryEquipmentRepository();
        var borrowings = new InMemoryBorrowingRepository(students, equipment);
        var first = new Borrowing(1, 101, DateTime.Today.AddDays(-2), FutureDate());
        var second = new Borrowing(2, 102, DateTime.Today.AddDays(-1), FutureDate());
        await borrowings.AddAsync(first);
        await borrowings.AddAsync(second);

        Assert.Equal(first.Id, Assert.Single(await borrowings.GetStudentHistoryAsync(1)).BorrowingId);
        Assert.Equal(second.Id, Assert.Single(await borrowings.GetStudentHistoryAsync(2)).BorrowingId);
        Assert.Empty(await borrowings.GetStudentHistoryAsync(3));
        Assert.Empty(await borrowings.GetStudentHistoryAsync(9999));
        Assert.Equal(first.Id, Assert.Single(await borrowings.GetEquipmentHistoryAsync(101)).BorrowingId);
        Assert.Equal(second.Id, Assert.Single(await borrowings.GetEquipmentHistoryAsync(102)).BorrowingId);
        Assert.Empty(await borrowings.GetEquipmentHistoryAsync(103));
        Assert.Empty(await borrowings.GetEquipmentHistoryAsync(9999));
    }

    [Fact]
    public async Task Equipment_management_cannot_edit_availability_and_history_uses_selected_item()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        await service.BorrowEquipmentAsync(1, 101, FutureDate());
        var management = new ManageEquipmentService(equipment);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => management.UpdateAsync(101, "EQ-102", "Duplicate identifier", null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => management.UpdateAsync(101, "EQ-101", " ", null));

        await management.UpdateAsync(101, "EQ-101-NEW", "Board renamed", "Description");

        var updated = await equipment.GetByIdAsync(101);
        Assert.NotNull(updated);
        Assert.False(updated.IsAvailable);
        Assert.Equal("Board renamed", updated.Name);
        Assert.Single(await borrowings.GetEquipmentHistoryAsync(101));
        Assert.Empty(await borrowings.GetEquipmentHistoryAsync(103));
    }

    [Fact]
    public async Task Borrowing_search_combines_term_status_and_sort_options()
    {
        var (students, equipment, borrowings, service) = CreateWorkflow();
        await service.BorrowEquipmentAsync(1, 101, FutureDate());
        var first = (await borrowings.GetAllAsync()).Single();
        await new ReturnEquipmentService(borrowings, equipment).ReturnEquipmentAsync(first.Id);
        await service.BorrowEquipmentAsync(1, 103, FutureDate());

        var returned = await borrowings.SearchAsync(new BorrowingSearchCriteria("s-0001", BorrowingStatusFilter.Returned));
        var active = await borrowings.SearchAsync(new BorrowingSearchCriteria("USB", BorrowingStatusFilter.Active));
        var byStudentName = await borrowings.SearchAsync(new BorrowingSearchCriteria(" JuAn ", BorrowingStatusFilter.Active));

        Assert.Single(returned);
        Assert.Equal(BorrowingStatus.Returned, returned[0].Status);
        Assert.Single(active);
        Assert.Equal("USB Webcam", active[0].EquipmentName);
        Assert.Single(byStudentName);
        Assert.Equal("Juan Dela Cruz", byStudentName[0].StudentName);
    }

    [Fact]
    public async Task Borrowing_search_sorts_both_directions_with_stable_identifier_ties()
    {
        var students = new InMemoryStudentRepository();
        var equipment = new InMemoryEquipmentRepository();
        var borrowings = new InMemoryBorrowingRepository(students, equipment);
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        await borrowings.AddAsync(new Borrowing(1, 101, DateTime.Today.AddDays(-3), DateTime.Today.AddDays(2), id: firstId));
        await borrowings.AddAsync(new Borrowing(1, 102, DateTime.Today.AddDays(-2), DateTime.Today.AddDays(1), id: secondId));
        await borrowings.AddAsync(new Borrowing(1, 103, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), id: thirdId));

        var ascending = await borrowings.SearchAsync(new BorrowingSearchCriteria(
            Status: BorrowingStatusFilter.All,
            SortField: BorrowingSortField.ExpectedReturnDate,
            Descending: false));
        var descending = await borrowings.SearchAsync(new BorrowingSearchCriteria(
            Status: BorrowingStatusFilter.All,
            SortField: BorrowingSortField.ExpectedReturnDate,
            Descending: true));
        var checkoutAscending = await borrowings.SearchAsync(new BorrowingSearchCriteria(
            Status: BorrowingStatusFilter.All,
            SortField: BorrowingSortField.CheckoutDate,
            Descending: false));
        var checkoutDescending = await borrowings.SearchAsync(new BorrowingSearchCriteria(
            Status: BorrowingStatusFilter.All,
            SortField: BorrowingSortField.CheckoutDate,
            Descending: true));
        var noMatches = await borrowings.SearchAsync(new BorrowingSearchCriteria("no matching student or equipment"));

        Assert.Equal(new[] { secondId, thirdId, firstId }, ascending.Select(item => item.BorrowingId));
        Assert.Equal(new[] { firstId, secondId, thirdId }, descending.Select(item => item.BorrowingId));
        Assert.Equal(new[] { firstId, secondId, thirdId }, checkoutAscending.Select(item => item.BorrowingId));
        Assert.Equal(new[] { thirdId, secondId, firstId }, checkoutDescending.Select(item => item.BorrowingId));
        Assert.Empty(noMatches);
    }

    private static (InMemoryStudentRepository, InMemoryEquipmentRepository, InMemoryBorrowingRepository, BorrowEquipmentService)
        CreateWorkflow()
    {
        var students = new InMemoryStudentRepository();
        var equipment = new InMemoryEquipmentRepository();
        var borrowings = new InMemoryBorrowingRepository(students, equipment);
        return (students, equipment, borrowings, new BorrowEquipmentService(students, equipment, borrowings));
    }

    private static DateTime FutureDate() => DateTime.Today.AddDays(7);
}
