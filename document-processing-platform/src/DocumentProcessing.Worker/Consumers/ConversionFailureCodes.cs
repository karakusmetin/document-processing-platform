namespace DocumentProcessing.Worker.Consumers;

internal static class ConversionFailureCodes
{
    public const string InvalidRequest =
        "conversion.invalid-request";

    public const string PermanentFailure =
        "conversion.permanent-failure";

    public const string RetryAttemptsExhausted =
        "conversion.retry-attempts-exhausted";

    public const string UnexpectedFailureAttemptsExhausted =
        "conversion.unexpected-failure-attempts-exhausted";
}