using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IStudentRepository
{
    Task<IEnumerable<Student>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Student?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNormalizedNumberAsync(
        string normalizedNumber,
        int? exceptStudentId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Student student, CancellationToken cancellationToken = default);

    Task UpdateAsync(Student student, CancellationToken cancellationToken = default);
}
