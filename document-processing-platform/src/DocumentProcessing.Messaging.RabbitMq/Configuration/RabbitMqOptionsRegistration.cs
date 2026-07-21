using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

internal static class RabbitMqOptionsRegistration
{
    public static IServiceCollection AddRabbitMqOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqConnectionOptions>()
            .Bind(configuration.GetSection(
                RabbitMqConnectionOptions.SectionName))
            .Validate(
                static options =>
                    options.HostNames is { Length: > 0 } &&
                    options.HostNames.All(
                        static host =>
                            !string.IsNullOrWhiteSpace(host)),
                "At least one RabbitMQ host name must be configured.")
            .Validate(
                static options =>
                    options.Port is > 0 and <= 65535,
                "RabbitMQ port must be between 1 and 65535.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(
                        options.VirtualHost),
                "RabbitMQ virtual host is required.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(
                        options.UserName),
                "RabbitMQ user name is required.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(
                        options.Password),
                "RabbitMQ password is required.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(
                        options.ClientProvidedName),
                "RabbitMQ client provided name is required.")
            .Validate(
                static options =>
                    options.NetworkRecoveryInterval >
                    TimeSpan.Zero,
                "Network recovery interval must be greater than zero.")
            .Validate(
                static options =>
                    options.RequestedConnectionTimeout >
                    TimeSpan.Zero,
                "Connection timeout must be greater than zero.")
            .Validate(
                static options =>
                    options.RequestedHeartbeat >
                    TimeSpan.Zero,
                "Requested heartbeat must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqPublisherOptions>()
            .Bind(configuration.GetSection(
                RabbitMqPublisherOptions.SectionName))
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(
                        options.ProducerName),
                "RabbitMQ publisher producer name is required.")
            .Validate(
                static options =>
                    options.ConfirmationTimeout > TimeSpan.Zero,
                "Publisher confirmation timeout must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqConsumerOptions>()
            .Bind(configuration.GetSection(
                RabbitMqConsumerOptions.SectionName))
            .Validate(
                static options => options.PrefetchCount > 0,
                "Consumer prefetch count must be greater than zero.")
            .Validate(
                static options => options.ConcurrentConsumerCount > 0,
                "Concurrent consumer count must be greater than zero.")
            .Validate(
                static options => options.ShutdownTimeout > TimeSpan.Zero,
                "Consumer shutdown timeout must be greater than zero.")
            .ValidateOnStart();

       services
            .AddOptions<RabbitMqRetryOptions>()
            .Bind(configuration.GetSection(
                RabbitMqRetryOptions.SectionName))
            .Validate(
                static options =>
                    options.MaximumAttempts >= 1,
                "MaximumAttempts must be at least one.")
            .Validate(
                static options =>
                    options.DelaySeconds is not null &&
                    options.DelaySeconds.All(
                        static delay => delay > 0),
                "Every retry delay must be greater than zero.")
            .Validate(
                static options =>
                    options.DelaySeconds is not null &&
                    options.DelaySeconds
                        .Distinct()
                        .Count() ==
                    options.DelaySeconds.Length,
                "Retry delays must be unique.")
            .Validate(
                static options =>
                    options.DelaySeconds is not null &&
                    options.DelaySeconds.Length ==
                    options.MaximumAttempts - 1,
                "Retry delay count must equal MaximumAttempts minus one.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqTopologyOptions>()
            .Bind(configuration.GetSection(
                RabbitMqTopologyOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.CommandExchange) &&
                    !string.IsNullOrWhiteSpace(
                        options.EventExchange) &&
                    !string.IsNullOrWhiteSpace(
                        options.RetryExchange) &&
                    !string.IsNullOrWhiteSpace(
                        options.DeadLetterExchange) &&
                    !string.IsNullOrWhiteSpace(
                        options.ConversionRequestQueue) &&
                    !string.IsNullOrWhiteSpace(
                        options.ConversionDeadLetterQueue) &&
                    !string.IsNullOrWhiteSpace(
                        options.ConversionRequestedRoutingKey) &&
                    !string.IsNullOrWhiteSpace(
                        options.ConversionCompletedRoutingKey) &&
                    !string.IsNullOrWhiteSpace(
                        options.ConversionFailedRoutingKey) &&
                    !string.IsNullOrWhiteSpace(
                        options.ConversionDeadLetterRoutingKey) &&
                    !string.IsNullOrWhiteSpace(
                        options.RetryQueuePrefix) &&
                    !string.IsNullOrWhiteSpace(
                        options.RetryRoutingKeyPrefix),
                "RabbitMQ topology names cannot be empty.")
            .Validate(
                options => Enum.IsDefined(options.QueueType),
                "RabbitMQ queue type is not supported.")
            .ValidateOnStart();

        return services;
    }
}