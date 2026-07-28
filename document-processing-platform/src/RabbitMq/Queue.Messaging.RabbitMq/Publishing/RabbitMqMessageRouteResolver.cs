using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqMessageRouteResolver :
    IRabbitMqMessageRouteResolver
{
    private readonly IReadOnlyDictionary<
        Type,
        RabbitMqMessageRoute> _routes;

    public RabbitMqMessageRouteResolver(
        IEnumerable<IRabbitMqMessageRouteRegistration>
            registrations)
    {
        Guard.NotNull(registrations, nameof(registrations));

        IRabbitMqMessageRouteRegistration[] registrationArray =
            registrations.ToArray();

        ValidateDuplicateRegistrations(
            registrationArray);

        _routes =
            registrationArray.ToDictionary(
                static registration =>
                    registration.MessageClrType,

                static registration =>
                    registration.Route);
    }

    public RabbitMqMessageRoute Resolve<TMessage>()
    {
        Type messageClrType =
            typeof(TMessage);

        if (_routes.TryGetValue(
                messageClrType,
                out RabbitMqMessageRoute? route))
        {
            return route;
        }

        throw new NotSupportedException(
            $"No RabbitMQ message route is registered for CLR " +
            $"message type '{messageClrType.FullName}'. " +
            $"Register the route with " +
            $"'AddRabbitMqMessageRoute<{messageClrType.Name}>'.");
    }

    private static void ValidateDuplicateRegistrations(
        IEnumerable<IRabbitMqMessageRouteRegistration>
            registrations)
    {
        Type[] duplicateTypes =
            registrations
                .GroupBy(
                    static registration =>
                        registration.MessageClrType)
                .Where(
                    static group =>
                        group.Count() > 1)
                .Select(
                    static group =>
                        group.Key)
                .ToArray();

        if (duplicateTypes.Length == 0)
        {
            return;
        }

        string duplicateTypeNames =
            string.Join(
                ", ",
                duplicateTypes.Select(
                    static type =>
                        type.FullName ?? type.Name));

        throw new InvalidOperationException(
            $"Multiple RabbitMQ routes were registered for the " +
            $"same CLR message type. Duplicate types: " +
            $"{duplicateTypeNames}.");
    }
}