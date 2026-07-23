namespace DocumentProcessing.Worker.Consumers;

internal sealed record ConsumerMessageHandlingResult(
    ConsumerMessageDisposition Disposition,
    string Reason)
{
    public static ConsumerMessageHandlingResult Acknowledge(
        string reason)
    {
        return new ConsumerMessageHandlingResult(
            ConsumerMessageDisposition.Acknowledge,
            reason);
    }

    public static ConsumerMessageHandlingResult DeadLetter(
        string reason)
    {
        return new ConsumerMessageHandlingResult(
            ConsumerMessageDisposition.DeadLetter,
            reason);
    }
}