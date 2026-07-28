namespace Queue.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqConnectionOptions
{
    public const string SectionName = "RabbitMq:Connection";

    public string[] HostNames { get; set; } = ["localhost"];

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ClientProvidedName { get; set; } = "document-processing-worker";
    
    public string CompatibilityMode { get; set; } = "Standard";

    public string QueueType { get; set; } = "classic";

    public bool AutomaticRecoveryEnabled { get; set; } = true;

    public bool TopologyRecoveryEnabled { get; set; } = true;

    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan RequestedConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(30);
}