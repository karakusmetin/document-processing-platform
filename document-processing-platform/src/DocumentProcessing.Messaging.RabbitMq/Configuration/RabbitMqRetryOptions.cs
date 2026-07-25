namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqRetryOptions
{
    public const string SectionName =
        "RabbitMq:Retry";

    /*
     * Değer configuration tarafından açıkça verilmelidir.
     */
    public int MaximumAttempts { get; set; }

    /*
     * Bind edilen collection property'lerine dolu varsayılan
     * değer vermiyoruz. Aksi hâlde configuration değerleriyle
     * birleşme riski oluşur.
     */
    public int[] DelaySeconds { get; set; } =
        [];
}