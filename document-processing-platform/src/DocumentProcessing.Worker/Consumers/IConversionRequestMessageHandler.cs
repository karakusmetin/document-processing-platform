namespace DocumentProcessing.Worker.Consumers;

internal interface IConversionRequestMessageHandler
{
    Task<ConsumerMessageHandlingResult> HandleAsync(
        ConversionRequestDelivery delivery,
        CancellationToken cancellationToken);
}