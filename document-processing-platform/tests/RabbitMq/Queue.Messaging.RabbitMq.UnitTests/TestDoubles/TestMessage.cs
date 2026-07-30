namespace Queue.Messaging.RabbitMq.UnitTests.TestDoubles;

internal sealed record TestMessage(
    string Value);

internal sealed record SecondTestMessage(
    string Value);