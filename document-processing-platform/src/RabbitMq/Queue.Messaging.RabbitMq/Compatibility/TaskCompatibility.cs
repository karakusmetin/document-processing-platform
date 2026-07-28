namespace Queue.Messaging.RabbitMq.Compatibility;

internal static class TaskCompatibility
{
    public static Task WaitAsync(
        Task task,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(
            task,
            nameof(task));

#if NET10_0_OR_GREATER
        return task.WaitAsync(
            cancellationToken);
#else
        return WaitCoreAsync(
            task,
            Timeout.InfiniteTimeSpan,
            cancellationToken);
#endif
    }

    public static Task WaitAsync(
        Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(
            task,
            nameof(task));

#if NET10_0_OR_GREATER
        return task.WaitAsync(
            timeout,
            cancellationToken);
#else
        return WaitCoreAsync(
            task,
            timeout,
            cancellationToken);
#endif
    }

    public static async Task<T> WaitAsync<T>(
        Task<T> task,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(
            task,
            nameof(task));

#if NET10_0_OR_GREATER
        return await task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
#else
        await WaitCoreAsync(
                task,
                Timeout.InfiniteTimeSpan,
                cancellationToken)
            .ConfigureAwait(false);

        return await task.ConfigureAwait(false);
#endif
    }

    public static async Task<T> WaitAsync<T>(
        Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(
            task,
            nameof(task));

#if NET10_0_OR_GREATER
        return await task
            .WaitAsync(
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
#else
        await WaitCoreAsync(
                task,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);

        return await task.ConfigureAwait(false);
#endif
    }

#if NET48
    private static async Task WaitCoreAsync(
        Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        if (timeout != Timeout.InfiniteTimeSpan &&
            timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timeout cannot be negative.");
        }

        using CancellationTokenSource linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        Task timeoutTask =
            timeout == Timeout.InfiniteTimeSpan
                ? Task.Delay(
                    Timeout.Infinite,
                    linkedSource.Token)
                : Task.Delay(
                    timeout,
                    linkedSource.Token);

        Task completedTask =
            await Task.WhenAny(
                    task,
                    timeoutTask)
                .ConfigureAwait(false);

        if (completedTask == task)
        {
            linkedSource.Cancel();

            await task.ConfigureAwait(false);
            return;
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        throw new TimeoutException(
            $"The operation did not complete within '{timeout}'.");
    }
#endif
}