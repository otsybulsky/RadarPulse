namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingAsyncExecutionOptions
{
    public static RadarProcessingAsyncExecutionOptions Default { get; } = new();

    public RadarProcessingAsyncExecutionOptions(
        int workerCount = 1,
        int queueCapacity = 1,
        RadarProcessingWorkerAffinity workerAffinity = RadarProcessingWorkerAffinity.Shard,
        RadarProcessingWorkerTimeoutPolicy timeoutPolicy = RadarProcessingWorkerTimeoutPolicy.Disabled,
        TimeSpan? batchTimeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);
        EnsureKnownWorkerAffinity(workerAffinity);
        EnsureKnownTimeoutPolicy(timeoutPolicy);
        ValidateTimeout(timeoutPolicy, batchTimeout);

        WorkerCount = workerCount;
        QueueCapacity = queueCapacity;
        WorkerAffinity = workerAffinity;
        TimeoutPolicy = timeoutPolicy;
        BatchTimeout = batchTimeout;
    }

    public int WorkerCount { get; }

    public int QueueCapacity { get; }

    public RadarProcessingWorkerAffinity WorkerAffinity { get; }

    public RadarProcessingWorkerTimeoutPolicy TimeoutPolicy { get; }

    public TimeSpan? BatchTimeout { get; }

    public bool HasBatchTimeout => BatchTimeout.HasValue;

    internal static void EnsureKnownWorkerAffinity(
        RadarProcessingWorkerAffinity workerAffinity)
    {
        if (workerAffinity is not RadarProcessingWorkerAffinity.None and
            not RadarProcessingWorkerAffinity.Shard)
        {
            throw new ArgumentOutOfRangeException(nameof(workerAffinity));
        }
    }

    internal static void EnsureKnownTimeoutPolicy(
        RadarProcessingWorkerTimeoutPolicy timeoutPolicy)
    {
        if (timeoutPolicy is not RadarProcessingWorkerTimeoutPolicy.Disabled and
            not RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy and
            not RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutPolicy));
        }
    }

    private static void ValidateTimeout(
        RadarProcessingWorkerTimeoutPolicy timeoutPolicy,
        TimeSpan? batchTimeout)
    {
        if (batchTimeout.HasValue && batchTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchTimeout),
                batchTimeout,
                "Batch timeout must be positive when specified.");
        }

        if (timeoutPolicy == RadarProcessingWorkerTimeoutPolicy.Disabled)
        {
            if (batchTimeout.HasValue)
            {
                throw new ArgumentException(
                    "Batch timeout requires a non-disabled timeout policy.",
                    nameof(batchTimeout));
            }

            return;
        }

        if (!batchTimeout.HasValue)
        {
            throw new ArgumentException(
                "A non-disabled timeout policy requires a batch timeout.",
                nameof(timeoutPolicy));
        }
    }
}
