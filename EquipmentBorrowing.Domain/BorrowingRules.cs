namespace EquipmentBorrowing.Domain;

public static class BorrowingRules
{
    public const int MaximumActiveBorrowings = 3;

    /// <summary>
    /// Borrowing dates are local calendar dates stored at UTC midnight. Do not convert
    /// these values between time zones; their date component is the user-visible date.
    /// </summary>
    public static DateTime NormalizeLocalCalendarDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    public static DateTime LocalToday => NormalizeLocalCalendarDate(DateTime.Today);

    public static bool IsExpectedReturnDateValid(DateTime expectedReturnDate) =>
        expectedReturnDate.Date > DateTime.Today;
}
