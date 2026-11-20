using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public sealed class InMemoryStudentRepository : IStudentRepository
{
    private readonly List<Student> _students;

    public InMemoryStudentRepository(IEnumerable<Student>? students = null)
    {
        _students = students?.ToList() ??
        [
            new Student(1, "S-0001", "Juan Dela Cruz", null, true, BorrowingRules.MaximumActiveBorrowings),
            new Student(2, "S-0002", "Mariaa Santos", null, true, BorrowingRules.MaximumActiveBorrowings),
            new Student(3, "S-0003", "Ted Balaba", null, true, BorrowingRules.MaximumActiveBorrowings)
        ];
    }

    public Task<IEnumerable<Student>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Student>>(_students.OrderBy(student => student.Name).ToArray());

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_students.FirstOrDefault(student => student.Id == id));

    public Task<bool> ExistsByNormalizedNumberAsync(
        string normalizedNumber,
        int? exceptStudentId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_students.Any(student => student.NormalizedStudentNumber == normalizedNumber && student.Id != exceptStudentId));

    public Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        if (_students.Any(existing => existing.NormalizedStudentNumber == student.NormalizedStudentNumber))
        {
            throw new InvalidOperationException("That institutional student number is already registered.");
        }

        student.AssignId(_students.Count == 0 ? 1 : _students.Max(existing => existing.Id) + 1);
        _students.Add(student);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        var index = _students.FindIndex(existing => existing.Id == student.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Student was not found.");
        }

        if (_students.Any(existing => existing.Id != student.Id && existing.NormalizedStudentNumber == student.NormalizedStudentNumber))
        {
            throw new InvalidOperationException("That institutional student number belongs to another student.");
        }

        _students[index] = student;
        return Task.CompletedTask;
    }
}
