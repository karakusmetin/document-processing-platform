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
                        static host => !string.IsNullOrWhiteSpace(host)),
                "At least one RabbitMQ host name must be configured.")
            .Validate(
                static options => options.Port is > 0 and <= 65535,
                "RabbitMQ port must be between 1 and 65535.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(options.VirtualHost),
                "RabbitMQ virtual host is required.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(options.UserName),
                "RabbitMQ user name is required.")
            .Validate(
                static options =>
                    !string.IsNullOrWhiteSpace(options.Password),
                "RabbitMQ password is required.")
            .Validate(
                static options =>
                    options.NetworkRecoveryInterval > TimeSpan.Zero,
                "Network recovery interval must be greater than zero.")
            .Validate(
                static options =>
                    options.RequestedConnectionTimeout > TimeSpan.Zero,
                "Connection timeout must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqPublisherOptions>()
            .Bind(configuration.GetSection(
                RabbitMqPublisherOptions.SectionName))
            .Validate(
                static options =>
                    options.ConfirmationTimeout > TimeSpan.Zero,
                "Publisher confirmation timeout must be greater than zero.")
            .Validate(
                static options => options.DeliveryMode is 1 or 2,
                "Delivery mode must be either 1 or 2.")
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
                static options => options.MaximumAttempts > 0,
                "Maximum retry attempt count must be greater than zero.")
            .Validate(
                static options =>
                    options.DelaySeconds is { Length: > 0 } &&
                    options.DelaySeconds.All(
                        static delay => delay > 0),
                "Every retry delay must be greater than zero.")
            .Validate(
                static options =>
                    options.MaximumAttempts ==
                    options.DelaySeconds.Length + 1,
                "MaximumAttempts must equal retry delay count plus one.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqTopologyOptions>()
            .Bind(configuration.GetSection(
                RabbitMqTopologyOptions.SectionName))
            .Validate(
                static options =>
                    HasValue(options.CommandExchange) &&
                    HasValue(options.EventExchange) &&
                    HasValue(options.DeadLetterExchange) &&
                    HasValue(options.ConversionRequestQueue) &&
                    HasValue(options.ConversionDeadLetterQueue) &&
                    HasValue(options.ConversionRequestedRoutingKey) &&
                    HasValue(options.ConversionCompletedRoutingKey) &&
                    HasValue(options.ConversionFailedRoutingKey) &&
                    HasValue(options.ConversionDeadLetterRoutingKey) &&
                    HasValue(options.RetryQueuePrefix),
                "RabbitMQ topology names cannot be empty.")
            .ValidateOnStart();

        return services;
    }

    private static bool HasValue(string? value) =>
        !string.IsNullOrWhiteSpace(value);
}