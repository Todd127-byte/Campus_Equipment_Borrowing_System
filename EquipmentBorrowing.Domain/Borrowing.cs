namespace EquipmentBorrowing.Domain;

public sealed class Borrowing
{
    public Guid Id { get; private set; }
    public Guid CheckoutBatchId { get; private set; }
    public int StudentId { get; private set; }
    public int EquipmentId { get; private set; }
    public DateTime DateBorrowed { get; private set; }
    public DateTime ExpectedReturnDate { get; private set; }
    public DateTime? ActualReturnDate { get; private set; }
    public BorrowingStatus Status { get; private set; }

    public Borrowing(
        int studentId,
        int equipmentId,
        DateTime dateBorrowed,
        DateTime expectedReturnDate,
        Guid? id = null,
        Guid? checkoutBatchId = null)
    {
        Id = id ?? Guid.NewGuid();
        CheckoutBatchId = checkoutBatchId ?? Guid.NewGuid();
        StudentId = studentId;
        EquipmentId = equipmentId;
        DateBorrowed = BorrowingRules.NormalizeLocalCalendarDate(dateBorrowed);
        ExpectedReturnDate = BorrowingRules.NormalizeLocalCalendarDate(expectedReturnDate);
        Status = BorrowingStatus.Active;
    }

    private Borrowing()
    {
    }

    public void MarkAsReturned(DateTime? actualReturnDate = null)
    {
        if (Status != BorrowingStatus.Active)
        {
            throw new InvalidOperationException("Only an active borrowing can be returned.");
        }

        var returnedAt = BorrowingRules.NormalizeLocalCalendarDate(actualReturnDate ?? DateTime.Today);
        if (returnedAt < DateBorrowed)
        {
            throw new InvalidOperationException("Actual return date cannot be earlier than checkout.");
        }

        ActualReturnDate = returnedAt;
        Status = BorrowingStatus.Returned;
    }
}
