namespace EquipmentBorrowing.Domain;

public sealed class Equipment
{
    public int Id { get; private set; }
    public string EquipmentIdentifier { get; private set; } = string.Empty;
    public string NormalizedIdentifier { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsAvailable { get; private set; }

    // Kept for existing Activity 2/3 sample data.
    public Equipment(int id, string name, bool isAvailable = true)
        : this(id, $"LEGACY-{id}", name, null, isAvailable)
    {
    }

    public Equipment(int id, string equipmentIdentifier, string name, string? description, bool isAvailable = true)
    {
        Id = id;
        SetInformation(equipmentIdentifier, name, description);
        IsAvailable = isAvailable;
    }

    public Equipment(string equipmentIdentifier, string name, string? description = null)
        : this(0, equipmentIdentifier, name, description)
    {
    }

    private Equipment()
    {
    }

    public void UpdateInformation(string equipmentIdentifier, string name, string? description)
    {
        SetInformation(equipmentIdentifier, name, description);
    }

    public void MarkAsBorrowed() => IsAvailable = false;
    public void MarkAsAvailable() => IsAvailable = true;

    public void AssignId(int id)
    {
        if (Id != 0 || id <= 0)
        {
            throw new InvalidOperationException("An equipment ID can only be assigned once.");
        }

        Id = id;
    }

    private void SetInformation(string equipmentIdentifier, string name, string? description)
    {
        EquipmentIdentifier = equipmentIdentifier.Trim();
        NormalizedIdentifier = IdentifierNormalizer.Normalize(EquipmentIdentifier);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
