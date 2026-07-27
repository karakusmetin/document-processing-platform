using DocumentProcessing.IntegrationTests.Messaging.PublishConsume;
using DocumentProcessing.IntegrationTests.Messaging.Reliability;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Consuming;
using Queue.Messaging.RabbitMq.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;

namespace DocumentProcessing.IntegrationTests.Infrastructure;

internal static class RabbitMqTestHostFactory
{
    public static IHost CreatePublishConsumeHost(
        string connectionString,
        RabbitMqIntegrationTestNames names,
        IntegrationTestMessageProbe probe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(probe);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(Array.Empty<string>());

        /*
         * Integration test yalnızca aşağıda verdiğimiz kontrollü
         * in-memory configuration ile çalışsın.
         *
         * appsettings.json, environment variable veya önceki array
         * değerlerinin birleşmesini engelliyoruz.
         */
        builder.Configuration.Sources.Clear();

        builder.Configuration
            .AddInMemoryCollection(
                CreateRabbitMqConfiguration(
                    connectionString));

        /*
         * Teste özel aynı isim ve probe instance'ları topology
         * ile handler tarafından kullanılacak.
         */
        builder.Services.AddSingleton(
            names);

        builder.Services.AddSingleton(
            probe);

        /*
         * Ortak RabbitMQ altyapısı ve topology initializer.
         */
        builder.Services
            .AddRabbitMqMessaging(
                builder.Configuration)
            .AddRabbitMqTopologyInitialization();
        RabbitMqRetryOptions? boundRetryOptions =
    builder.Configuration
        .GetSection(
            RabbitMqRetryOptions.SectionName)
        .Get<RabbitMqRetryOptions>();

        Console.WriteLine(
            $"MaximumAttempts: " +
            $"{boundRetryOptions?.MaximumAttempts}");

        Console.WriteLine(
            $"DelaySeconds: " +
            $"{string.Join(
                ", ",
                boundRetryOptions?.DelaySeconds ??
                [])}");

        Console.WriteLine(
            $"DelayCount: " +
            $"{boundRetryOptions?.DelaySeconds.Length}");
        /*
         * Test uygulamasına ait topology.
         */
        builder.Services
            .AddRabbitMqTopologyDefinition<
                IntegrationTestTopologyDefinition>();

        /*
         * Test mesajının publish rotası.
         */
        builder.Services
            .AddRabbitMqMessageRoute<
                IntegrationTestRequested>(
                route =>
                {
                    route.Exchange =
                        names.ExchangeName;

                    route.RoutingKey =
                        names.RoutingKey;

                    route.MessageType =
                        IntegrationTestMessageContracts
                            .RequestedMessageType;

                    route.MessageVersion =
                        IntegrationTestMessageContracts.Version;
                });

        /*
         * Test mesajının generic consumer kaydı.
         */
        builder.Services
            .AddRabbitMqConsumer<
                IntegrationTestRequested,
                IntegrationTestRequestedHandler>(
                consumer =>
                {
                    consumer.QueueName =
                        names.QueueName;

                    consumer.MessageType =
                        IntegrationTestMessageContracts
                            .RequestedMessageType;

                    consumer.MessageVersion =
                        IntegrationTestMessageContracts.Version;

                    consumer.ConsumerTagPrefix =
                        "integration-publish-consume";
                });

        return builder.Build();
    }
    public static IHost CreateReliabilityHost<THandler>(
    string connectionString,
    RabbitMqReliabilityTestNames names,
    ReliabilityMessageProbe probe)
    where THandler :
        class,
        IRabbitMqMessageHandler<ReliabilityTestRequested>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(probe);

        HostApplicationBuilder builder =
            Host.CreateApplicationBuilder(
                Array.Empty<string>());

        builder.Configuration.Sources.Clear();

        builder.Configuration.AddInMemoryCollection(
            CreateRabbitMqConfiguration(
                connectionString));

        builder.Services.AddSingleton(
            names);

        builder.Services.AddSingleton(
            probe);

        builder.Services
            .AddRabbitMqMessaging(
                builder.Configuration)
            .AddRabbitMqTopologyInitialization();

        builder.Services
            .AddRabbitMqTopologyDefinition<
                ReliabilityTestTopologyDefinition>();

        builder.Services
            .AddRabbitMqMessageRoute<
                ReliabilityTestRequested>(
                route =>
                {
                    route.Exchange =
                        names.CommandExchange;

                    route.RoutingKey =
                        names.RequestedRoutingKey;

                    route.MessageType =
                        ReliabilityTestMessageContracts
                            .RequestedMessageType;

                    route.MessageVersion =
                        ReliabilityTestMessageContracts.Version;

                    route.RetryExchange =
                        names.RetryExchange;

                    route.RetryRoutingKeyPrefix =
                        names.RetryRoutingKeyPrefix;
                });

        builder.Services
            .AddRabbitMqConsumer<
                ReliabilityTestRequested,
                THandler>(
                consumer =>
                {
                    consumer.QueueName =
                        names.RequestQueue;

                    consumer.MessageType =
                        ReliabilityTestMessageContracts
                            .RequestedMessageType;

                    consumer.MessageVersion =
                        ReliabilityTestMessageContracts.Version;

                    consumer.ConsumerTagPrefix =
                        "integration-reliability";
                });

        return builder.Build();
    }
    private static IReadOnlyDictionary<string, string?>
        CreateRabbitMqConfiguration(
            string connectionString)
    {
        Uri connectionUri =
            new(connectionString);

        (string userName, string password) =
            ResolveCredentials(
                connectionUri);

        string virtualHost =
            ResolveVirtualHost(
                connectionUri);

        return new Dictionary<string, string?>
        {
            ["RabbitMq:Connection:HostNames:0"] =
                connectionUri.Host,

            ["RabbitMq:Connection:Port"] =
                connectionUri.Port.ToString(
                    CultureInfo.InvariantCulture),

            ["RabbitMq:Connection:VirtualHost"] =
                virtualHost,

            ["RabbitMq:Connection:UserName"] =
                userName,

            ["RabbitMq:Connection:Password"] =
                password,

            ["RabbitMq:Connection:ClientProvidedName"] =
                "document-processing-integration-tests",

            ["RabbitMq:Connection:AutomaticRecoveryEnabled"] =
                bool.TrueString,

            ["RabbitMq:Connection:TopologyRecoveryEnabled"] =
                bool.TrueString,

            ["RabbitMq:Connection:NetworkRecoveryInterval"] =
                "00:00:01",

            ["RabbitMq:Connection:RequestedConnectionTimeout"] =
                "00:00:10",

            ["RabbitMq:Connection:RequestedHeartbeat"] =
                "00:00:10",

            ["RabbitMq:Publisher:ProducerName"] =
                "document-processing-integration-tests",

            ["RabbitMq:Publisher:ConfirmationTimeout"] =
                "00:00:10",

            ["RabbitMq:Consumer:PrefetchCount"] =
                "1",

            ["RabbitMq:Consumer:ConcurrentConsumerCount"] =
                "1",

            ["RabbitMq:Consumer:AutoAcknowledgement"] =
                bool.FalseString,

            ["RabbitMq:Consumer:ConsumerTagPrefix"] =
                "document-processing-integration-tests",

            ["RabbitMq:Consumer:ShutdownTimeout"] =
                "00:00:10",

            /*
             * Bu test retry kullanmıyor fakat ortak options
             * validation başlangıçta çalıştığı için geçerli bir
             * retry ayarı veriyoruz.
             */
            ["RabbitMq:Retry:MaximumAttempts"] = "4",

            ["RabbitMq:Retry:DelaySeconds:0"] = "1",

            ["RabbitMq:Retry:DelaySeconds:1"] = "2",

            ["RabbitMq:Retry:DelaySeconds:2"] = "3",

            ["RabbitMq:Topology:QueueType"] =
                "Classic"
        };
    }

    private static (
        string UserName,
        string Password)
        ResolveCredentials(
            Uri connectionUri)
    {
        string decodedUserInfo =
            Uri.UnescapeDataString(
                connectionUri.UserInfo);

        string[] values =
            decodedUserInfo.Split(
                ':',
                count: 2);

        if (values.Length != 2 ||
            string.IsNullOrWhiteSpace(values[0]))
        {
            throw new InvalidOperationException(
                "RabbitMQ integration test connection string " +
                "does not contain valid credentials.");
        }

        return (
            UserName:
                values[0],

            Password:
                values[1]);
    }

    private static string ResolveVirtualHost(
        Uri connectionUri)
    {
        if (string.IsNullOrWhiteSpace(
                connectionUri.AbsolutePath) ||
            connectionUri.AbsolutePath == "/")
        {
            return "/";
        }

        return Uri.UnescapeDataString(
            connectionUri.AbsolutePath
                .TrimStart('/'));
    }
}