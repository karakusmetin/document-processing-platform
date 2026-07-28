namespace Queue.Messaging.RabbitMq.Compatibility;

internal static class EnumCompatibility
{
    public static bool IsDefined<TEnum>(
        TEnum value)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(
            typeof(TEnum),
            value);
    }
}