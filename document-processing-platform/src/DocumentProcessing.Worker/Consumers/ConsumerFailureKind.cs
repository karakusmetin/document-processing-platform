namespace DocumentProcessing.Worker.Consumers;

internal enum ConsumerFailureKind
{
    None = 0,

    //JSON deserialize edilemedi.
    MalformedMessage = 1,

    //MessageId, Attempt, Payload veya zorunlu alanlar geçersiz.
    InvalidEnvelope = 2,

    //Conversion queue’ya yanlış mesaj türü gönderilmiş.
    UnsupportedMessageType = 3,

    // Consumer’ın desteklemediği contract sürümü gelmiş.
    UnsupportedMessageVersion = 4,

    //Provider hatanın tekrar denenmemesi gerektiğini söyledi.
    PermanentConversionFailure = 5,

    //Retryable hata vardı ama bütün denemeler tükendi.
    RetryAttemptsExhausted = 6
}