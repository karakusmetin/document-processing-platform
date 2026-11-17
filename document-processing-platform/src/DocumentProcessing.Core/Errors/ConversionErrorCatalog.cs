namespace DocumentProcessing.Core.Errors;

public static class ConversionErrorCatalog
{
    private static readonly IReadOnlyDictionary<string, ConversionErrorDefinition> Definitions =
        new Dictionary<string, ConversionErrorDefinition>(StringComparer.Ordinal)
        {
            [ConversionErrorCodes.InvalidRequest] =
                new(ConversionErrorCodes.InvalidRequest, false),
            [ConversionErrorCodes.SourceNotFound] =
                new(ConversionErrorCodes.SourceNotFound, false),
            [ConversionErrorCodes.SourceAccessDenied] =
                new(ConversionErrorCodes.SourceAccessDenied, false),
            [ConversionErrorCodes.SourceReadFailed] =
                new(ConversionErrorCodes.SourceReadFailed, true),
            [ConversionErrorCodes.UnsupportedFormat] =
                new(ConversionErrorCodes.UnsupportedFormat, false),
            [ConversionErrorCodes.SourceFileCorrupted] =
                new(ConversionErrorCodes.SourceFileCorrupted, false),
            [ConversionErrorCodes.ProviderUnavailable] =
                new(ConversionErrorCodes.ProviderUnavailable, true),
            [ConversionErrorCodes.RuntimeNotFound] =
                new(ConversionErrorCodes.RuntimeNotFound, false),
            [ConversionErrorCodes.InputNotFound] =
                new(ConversionErrorCodes.InputNotFound, false),
            [ConversionErrorCodes.ProcessStartFailed] =
                new(ConversionErrorCodes.ProcessStartFailed, true),
            [ConversionErrorCodes.ConversionTimeout] =
                new(ConversionErrorCodes.ConversionTimeout, true),
            [ConversionErrorCodes.EngineCrashed] =
                new(ConversionErrorCodes.EngineCrashed, true),
            [ConversionErrorCodes.OutputNotProduced] =
                new(ConversionErrorCodes.OutputNotProduced, true),
            [ConversionErrorCodes.ConversionOutputInvalid] =
                new(ConversionErrorCodes.ConversionOutputInvalid, false),
            [ConversionErrorCodes.ConversionIoFailed] =
                new(ConversionErrorCodes.ConversionIoFailed, true),
            [ConversionErrorCodes.ArtifactWriteFailed] =
                new(ConversionErrorCodes.ArtifactWriteFailed, true),
            [ConversionErrorCodes.Unexpected] =
                new(ConversionErrorCodes.Unexpected, false)
        };

    public static ConversionErrorDefinition Get(string? code)
    {
        if (code is not null && Definitions.TryGetValue(code, out ConversionErrorDefinition? definition))
        {
            return definition;
        }

        return Definitions[ConversionErrorCodes.Unexpected];
    }
}
