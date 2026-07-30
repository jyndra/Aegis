namespace Aegis.Core.Errors;

/// <summary>
/// Custom exception carrying a system-wide Aegis error code.
/// </summary>
public class AegisException : Exception
{
    public string ErrorCode { get; }

    public AegisException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public AegisException(string errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
