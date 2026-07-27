namespace Queue.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName ="RabbitMq:Topology";

    /// <summary>
    /// Topology builder tarafından varsayılan olarak
    /// oluşturulacak queue türüdür.
    /// </summary>
    public RabbitMqQueueType QueueType { get; set; } = RabbitMqQueueType.Classic;
}