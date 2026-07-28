namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

internal sealed record ReliabilityTestRequested
{
    public required Guid Id { get; init; }

    public required string Value { get; init; }
}