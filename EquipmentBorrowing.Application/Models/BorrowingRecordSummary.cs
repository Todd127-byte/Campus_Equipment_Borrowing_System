using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Models;

public sealed record BorrowingRecordSummary(
    Guid BorrowingId,
    int StudentId,
    string StudentNumber,
    string StudentName,
    int EquipmentId,
    string EquipmentIdentifier,
    string EquipmentName,
    DateTime DateBorrowed,
    DateTime ExpectedReturnDate,
    DateTime? ActualReturnDate,
    BorrowingStatus Status);
