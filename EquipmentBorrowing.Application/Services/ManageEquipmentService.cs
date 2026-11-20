using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public sealed class ManageEquipmentService(IEquipmentRepository equipmentRepository)
{
    public Task<IEnumerable<Equipment>> GetAllAsync(CancellationToken cancellationToken = default) =>
        equipmentRepository.GetAllAsync(cancellationToken);

    public Task<IEnumerable<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        equipmentRepository.GetAvailableAsync(cancellationToken);

    public async Task<int> RegisterAsync(
        string identifier,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(identifier, name);
        var normalized = IdentifierNormalizer.Normalize(identifier);
        if (await equipmentRepository.ExistsByNormalizedIdentifierAsync(normalized, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("That equipment identifier is already registered.");
        }

        var equipment = new Equipment(identifier, name, description);
        await equipmentRepository.AddAsync(equipment, cancellationToken);
        return equipment.Id;
    }

    public async Task UpdateAsync(
        int equipmentId,
        string identifier,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(identifier, name);
        var equipment = await equipmentRepository.GetByIdAsync(equipmentId, cancellationToken)
            ?? throw new InvalidOperationException("Equipment was not found. Refresh the list and try again.");
        var normalized = IdentifierNormalizer.Normalize(identifier);
        if (await equipmentRepository.ExistsByNormalizedIdentifierAsync(normalized, equipmentId, cancellationToken))
        {
            throw new InvalidOperationException("That equipment identifier belongs to another item.");
        }

        equipment.UpdateInformation(identifier, name, description);
        await equipmentRepository.UpdateAsync(equipment, cancellationToken);
    }

    private static void ValidateRequired(string identifier, string name)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Equipment identifier and name are required.");
        }
    }
}
