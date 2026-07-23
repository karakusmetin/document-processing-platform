namespace DocumentProcessing.Worker.Consumers;

internal enum ConsumerMessageDisposition
{
    Acknowledge = 1,
    DeadLetter = 2
}