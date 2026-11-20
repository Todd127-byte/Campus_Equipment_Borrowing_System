namespace EquipmentBorrowing.Application.Exceptions;

public sealed class OperationNotCommittedException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);
