namespace DocumentProcessing.Worker.Consumers;

internal enum ConsumerMessageDisposition
{
    Acknowledge = 1,
    Requeue = 2,
    DeadLetter = 3
}