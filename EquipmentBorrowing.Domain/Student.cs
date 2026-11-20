namespace EquipmentBorrowing.Domain;

public sealed class Student
{
    public int Id { get; private set; }
    public string InstitutionalStudentNumber { get; private set; } = string.Empty;
    public string NormalizedStudentNumber { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ContactInformation { get; private set; }
    public bool IsAllowedToBorrow { get; private set; }
    public int MaximumActiveBorrowings { get; private set; }

    // Kept for existing Activity 2/3 sample data. New registrations use a generated ID.
    public Student(int id, string name, bool isAllowedToBorrow, int maximumActiveBorrowings)
        : this(id, $"LEGACY-{id}", name, null, isAllowedToBorrow, maximumActiveBorrowings)
    {
    }

    public Student(
        int id,
        string institutionalStudentNumber,
        string name,
        string? contactInformation,
        bool isAllowedToBorrow,
        int maximumActiveBorrowings = BorrowingRules.MaximumActiveBorrowings)
    {
        Id = id;
        SetInformation(institutionalStudentNumber, name, contactInformation);
        IsAllowedToBorrow = isAllowedToBorrow;
        MaximumActiveBorrowings = maximumActiveBorrowings;
    }

    public Student(string institutionalStudentNumber, string name, string? contactInformation = null)
        : this(0, institutionalStudentNumber, name, contactInformation, true)
    {
    }

    private Student()
    {
    }

    public void UpdateInformation(string institutionalStudentNumber, string name, string? contactInformation)
    {
        SetInformation(institutionalStudentNumber, name, contactInformation);
    }

    public void SetBorrowingEligibility(bool isAllowedToBorrow) => IsAllowedToBorrow = isAllowedToBorrow;

    public void AssignId(int id)
    {
        if (Id != 0 || id <= 0)
        {
            throw new InvalidOperationException("A student ID can only be assigned once.");
        }

        Id = id;
    }

    private void SetInformation(string institutionalStudentNumber, string name, string? contactInformation)
    {
        InstitutionalStudentNumber = institutionalStudentNumber.Trim();
        NormalizedStudentNumber = IdentifierNormalizer.Normalize(InstitutionalStudentNumber);
        Name = name.Trim();
        ContactInformation = string.IsNullOrWhiteSpace(contactInformation) ? null : contactInformation.Trim();
    }
}
