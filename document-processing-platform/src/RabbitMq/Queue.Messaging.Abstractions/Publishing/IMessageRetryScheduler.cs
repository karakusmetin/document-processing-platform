namespace Queue.Messaging.Abstractions;

public interface IMessageRetryScheduler
{
    Task ScheduleRetryAsync<TMessage>(
        MessageEnvelope<TMessage> originalEnvelope,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}