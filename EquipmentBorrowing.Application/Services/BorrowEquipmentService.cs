using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public sealed class BorrowEquipmentService(
    IStudentRepository studentRepository,
    IEquipmentRepository equipmentRepository,
    IBorrowingRepository borrowingRepository)
{
    public async Task BorrowEquipmentAsync(
        int studentId,
        int equipmentId,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        await CheckoutAsync(studentId, [equipmentId], expectedReturnDate, multiple: false, cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> BorrowMultipleEquipmentAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default) =>
        CheckoutAsync(studentId, equipmentIds, expectedReturnDate, multiple: true, cancellationToken);

    private async Task<IReadOnlyList<Guid>> CheckoutAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        bool multiple,
        CancellationToken cancellationToken)
    {
        if (!BorrowingRules.IsExpectedReturnDateValid(expectedReturnDate))
        {
            throw new InvalidOperationException("Expected return date must be after today.");
        }

        if (equipmentIds.Count == 0 || (multiple && equipmentIds.Count < 2))
        {
            throw new InvalidOperationException("Multiple checkout requires at least two equipment items.");
        }

        if (equipmentIds.Distinct().Count() != equipmentIds.Count)
        {
            throw new InvalidOperationException("Select each equipment item only once.");
        }

        var student = await studentRepository.GetByIdAsync(studentId, cancellationToken)
            ?? throw new InvalidOperationException("Student does not exist.");
        if (!student.IsAllowedToBorrow)
        {
            throw new InvalidOperationException("Student is not allowed to borrow equipment.");
        }

        foreach (var equipmentId in equipmentIds)
        {
            var equipment = await equipmentRepository.GetByIdAsync(equipmentId, cancellationToken)
                ?? throw new InvalidOperationException($"Equipment #{equipmentId} does not exist.");
            if (!equipment.IsAvailable)
            {
                throw new InvalidOperationException($"{equipment.Name} is not available.");
            }
        }

        var activeBorrowings = await borrowingRepository.CountActiveByStudentIdAsync(studentId, cancellationToken);
        if (activeBorrowings + equipmentIds.Count > student.MaximumActiveBorrowings)
        {
            throw new InvalidOperationException("This checkout would exceed the student's maximum of three active borrowings.");
        }

        var expectedReturnUtc = BorrowingRules.NormalizeLocalCalendarDate(expectedReturnDate);
        return await borrowingRepository.CreateCheckoutAsync(
            student.Id,
            equipmentIds,
            expectedReturnUtc,
            cancellationToken);
    }
}
