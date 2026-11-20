using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Models;

public enum BorrowingStatusFilter
{
    All,
    Active,
    Returned
}

public enum BorrowingSortField
{
    CheckoutDate,
    ExpectedReturnDate
}

public sealed record BorrowingSearchCriteria(
    string? SearchText = null,
    BorrowingStatusFilter Status = BorrowingStatusFilter.All,
    BorrowingSortField SortField = BorrowingSortField.CheckoutDate,
    bool Descending = true);
