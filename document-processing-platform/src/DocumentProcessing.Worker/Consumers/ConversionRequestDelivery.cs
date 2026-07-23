namespace DocumentProcessing.Worker.Consumers;

internal sealed record ConversionRequestDelivery(
    byte[] Body,
    bool Redelivered,
    string Exchange,
    string RoutingKey);