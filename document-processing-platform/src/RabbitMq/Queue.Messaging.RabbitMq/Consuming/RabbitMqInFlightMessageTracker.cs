namespace Queue.Messaging.RabbitMq.Consuming;

internal sealed class RabbitMqInFlightMessageTracker
{
    private readonly object _syncRoot = new();

    private int _activeCount;

    private TaskCompletionSource<bool> _drained =
        CreateCompletedSource();

    public int ActiveCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeCount;
            }
        }
    }

    public IDisposable Track()
    {
        lock (_syncRoot)
        {
            if (_activeCount == 0)
            {
                _drained =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions
                            .RunContinuationsAsynchronously);
            }

            checked
            {
                _activeCount++;
            }
        }

        return new TrackingLease(this);
    }

    public Task WaitForDrainAsync(
        CancellationToken cancellationToken)
    {
        Task drainTask;

        lock (_syncRoot)
        {
            drainTask = _drained.Task;
        }

        return drainTask.WaitAsync(
            cancellationToken);
    }

    private void Release()
    {
        TaskCompletionSource<bool>? completionSource = null;

        lock (_syncRoot)
        {
            if (_activeCount <= 0)
            {
                throw new InvalidOperationException(
                    "No active RabbitMQ delivery is being tracked.");
            }

            _activeCount--;

            if (_activeCount == 0)
            {
                completionSource = _drained;
            }
        }

        completionSource?.TrySetResult(true);
    }

    private static TaskCompletionSource<bool>
        CreateCompletedSource()
    {
        TaskCompletionSource<bool> source =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        source.SetResult(true);

        return source;
    }

    private sealed class TrackingLease :
        IDisposable
    {
        private RabbitMqInFlightMessageTracker? _owner;

        public TrackingLease(
            RabbitMqInFlightMessageTracker owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            _owner = owner;
        }

        public void Dispose()
        {
            RabbitMqInFlightMessageTracker? owner =
                Interlocked.Exchange(
                    ref _owner,
                    null);

            owner?.Release();
        }
    }
}