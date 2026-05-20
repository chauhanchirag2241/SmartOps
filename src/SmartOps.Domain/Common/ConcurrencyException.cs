namespace SmartOps.Domain.Common;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message)
        : base(message)
    {
    }
}
