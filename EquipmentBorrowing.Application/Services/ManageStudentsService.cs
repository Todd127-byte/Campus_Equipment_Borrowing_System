using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public sealed class ManageStudentsService(IStudentRepository studentRepository)
{
    public Task<IEnumerable<Student>> GetAllAsync(CancellationToken cancellationToken = default) =>
        studentRepository.GetAllAsync(cancellationToken);

    public async Task<int> RegisterAsync(
        string institutionalStudentNumber,
        string name,
        string? contactInformation,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(institutionalStudentNumber, name);
        ValidateRegistrationContact(contactInformation);
        var normalizedNumber = IdentifierNormalizer.Normalize(institutionalStudentNumber);
        if (await studentRepository.ExistsByNormalizedNumberAsync(normalizedNumber, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("That institutional student number is already registered.");
        }

        var student = new Student(institutionalStudentNumber, name, contactInformation);
        await studentRepository.AddAsync(student, cancellationToken);
        return student.Id;
    }

    public async Task UpdateAsync(
        int studentId,
        string institutionalStudentNumber,
        string name,
        string? contactInformation,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(institutionalStudentNumber, name);
        var student = await studentRepository.GetByIdAsync(studentId, cancellationToken)
            ?? throw new InvalidOperationException("Student was not found. Refresh the list and try again.");
        var normalizedNumber = IdentifierNormalizer.Normalize(institutionalStudentNumber);
        if (await studentRepository.ExistsByNormalizedNumberAsync(normalizedNumber, studentId, cancellationToken))
        {
            throw new InvalidOperationException("That institutional student number belongs to another student.");
        }

        student.UpdateInformation(institutionalStudentNumber, name, contactInformation);
        await studentRepository.UpdateAsync(student, cancellationToken);
    }

    public async Task SetEligibilityAsync(
        int studentId,
        bool isEligible,
        CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetByIdAsync(studentId, cancellationToken)
            ?? throw new InvalidOperationException("Student was not found. Refresh the list and try again.");
        if (student.IsAllowedToBorrow == isEligible)
        {
            return;
        }

        student.SetBorrowingEligibility(isEligible);
        await studentRepository.UpdateAsync(student, cancellationToken);
    }

    private static void ValidateRequired(string number, string name)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Institutional student number and full name are required.");
        }
    }

    private static void ValidateRegistrationContact(string? contactInformation)
    {
        if (!string.IsNullOrEmpty(contactInformation) && string.IsNullOrWhiteSpace(contactInformation))
        {
            throw new InvalidOperationException("Contact information cannot contain only whitespace.");
        }
    }
}
