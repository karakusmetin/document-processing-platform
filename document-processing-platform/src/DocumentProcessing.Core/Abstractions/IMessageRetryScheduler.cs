using DocumentProcessing.Contracts.Messaging;

namespace DocumentProcessing.Core.Abstractions;

public interface IMessageRetryScheduler
{
    Task ScheduleRetryAsync<TMessage>(
        MessageEnvelope<TMessage> originalEnvelope,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}