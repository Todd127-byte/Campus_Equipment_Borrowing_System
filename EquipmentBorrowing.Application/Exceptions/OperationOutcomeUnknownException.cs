namespace EquipmentBorrowing.Application.Exceptions;

public sealed class OperationOutcomeUnknownException(Guid operationId, string operationName, Exception? inner = null)
    : Exception(
        $"The {operationName} could not be confirmed after the database connection failed. The operation was not retried. Refresh the lists and inspect the database before deciding what to do next.",
        inner)
{
    public Guid OperationId { get; } = operationId;
    public string OperationName { get; } = operationName;
}
