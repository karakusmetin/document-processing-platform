namespace Queue.Messaging.Abstractions;

internal static class Guard
{
    public static void NotNull(
        object? value,
        string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(
                parameterName);
        }
    }

    public static string NotNullOrWhiteSpace(
        string? value,
        string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(
                parameterName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value cannot be empty or contain only whitespace.",
                parameterName);
        }

        return value;
    }
}