namespace EquipmentBorrowing.Domain;

public static class IdentifierNormalizer
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
