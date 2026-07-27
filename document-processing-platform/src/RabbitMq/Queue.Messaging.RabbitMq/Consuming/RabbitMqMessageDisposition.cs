namespace Queue.Messaging.RabbitMq.Consuming;

/// <summary>
/// Bir RabbitMQ teslimatının handler tarafından nasıl
/// sonuçlandırılması gerektiğini belirtir.
/// </summary>
public enum RabbitMqMessageDisposition
{
    /// <summary>
    /// Mesajın işlenmesi tamamlandı veya güvenli biçimde başka
    /// bir mesaja dönüştürüldü. Orijinal teslimat ACK edilebilir.
    /// </summary>
    Acknowledge = 1,

    /// <summary>
    /// Mesaj tekrar işlenmemelidir. Orijinal teslimat
    /// NACK/requeue:false ile dead-letter akışına gönderilmelidir.
    /// </summary>
    DeadLetter = 2
}