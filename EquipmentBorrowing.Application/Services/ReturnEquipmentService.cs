using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public sealed class ReturnEquipmentService
{
    private readonly IBorrowingRepository _borrowingRepository;

    public ReturnEquipmentService(IBorrowingRepository borrowingRepository, IEquipmentRepository? unused = null)
    {
        _borrowingRepository = borrowingRepository;
    }

    public Task ReturnEquipmentAsync(Guid borrowingId, CancellationToken cancellationToken = default) =>
        _borrowingRepository.ReturnAsync(borrowingId, BorrowingRules.LocalToday, cancellationToken);
}
