using Queue.Messaging.Abstractions;
using Queue.Messaging.RabbitMq.Consuming;

namespace Queue.Messaging.RabbitMq.UnitTests.TestDoubles;

internal sealed class TestMessageHandler :
    IRabbitMqMessageHandler<TestMessage>
{
    public Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<TestMessage> envelope,
        RabbitMqDeliveryContext deliveryContext,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            "This handler is used only by dependency injection tests.");
    }
}

internal sealed class SecondTestMessageHandler :
    IRabbitMqMessageHandler<SecondTestMessage>
{
    public Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<SecondTestMessage> envelope,
        RabbitMqDeliveryContext deliveryContext,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            "This handler is used only by dependency injection tests.");
    }
}