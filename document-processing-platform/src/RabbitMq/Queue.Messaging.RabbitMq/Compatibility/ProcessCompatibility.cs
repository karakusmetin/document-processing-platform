using System.Diagnostics;

namespace Queue.Messaging.RabbitMq.Compatibility;

internal static class ProcessCompatibility
{
    public static int CurrentProcessId
    {
        get
        {
#if NET10_0_OR_GREATER
            return ProcessCompatibility.CurrentProcessId;
#else
            using Process process =
                Process.GetCurrentProcess();

            return process.Id;
#endif
        }
    }
}