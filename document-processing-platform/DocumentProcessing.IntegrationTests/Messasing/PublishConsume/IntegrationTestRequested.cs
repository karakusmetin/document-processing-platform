namespace DocumentProcessing.IntegrationTests
    .Messaging.PublishConsume;

internal sealed record IntegrationTestRequested
{
    public required Guid Id { get; init; }

    public required string Value { get; init; }
}