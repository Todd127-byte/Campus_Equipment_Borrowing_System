using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public sealed class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipment =
    [
        new Equipment(101, "EQ-101", "Arduino Uno", null),
        new Equipment(102, "EQ-102", "Raspberry Pi", null),
        new Equipment(103, "EQ-103", "USB Webcam", null),
        new Equipment(104, "EQ-104", "Portable Projector", null),
        new Equipment(105, "EQ-105", "Spare Monitor", null)
    ];

    public Task<IEnumerable<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Equipment>>(_equipment.Where(item => item.IsAvailable).OrderBy(item => item.Name).ToArray());

    public Task<IEnumerable<Equipment>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Equipment>>(_equipment.OrderBy(item => item.Id).ToArray());

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_equipment.FirstOrDefault(item => item.Id == id));

    public Task<bool> ExistsByNormalizedIdentifierAsync(
        string normalizedIdentifier,
        int? exceptEquipmentId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_equipment.Any(item => item.NormalizedIdentifier == normalizedIdentifier && item.Id != exceptEquipmentId));

    public Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        if (_equipment.Any(existing => existing.NormalizedIdentifier == equipment.NormalizedIdentifier))
        {
            throw new InvalidOperationException("That equipment identifier is already registered.");
        }

        equipment.AssignId(_equipment.Count == 0 ? 1 : _equipment.Max(existing => existing.Id) + 1);
        _equipment.Add(equipment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        var index = _equipment.FindIndex(existing => existing.Id == equipment.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Equipment was not found.");
        }

        if (_equipment.Any(existing => existing.Id != equipment.Id && existing.NormalizedIdentifier == equipment.NormalizedIdentifier))
        {
            throw new InvalidOperationException("That equipment identifier belongs to another item.");
        }

        _equipment[index] = equipment;
        return Task.CompletedTask;
    }
}
