namespace SmartOps.Shared.Common;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message)
        : base(message)
    {
    }
}
