namespace DocumentProcessing.Core.Errors;

public sealed class ConverterOperationException : Exception
{
    public string ErrorCode { get; }

    public ConverterOperationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public ConverterOperationException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
