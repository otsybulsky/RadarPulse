namespace RadarPulse.Domain.Processing;

/// <summary>
/// Configures async shard transport worker count, queue capacity, affinity, and timeout behavior.
/// </summary>
public sealed record RadarProcessingAsyncExecutionOptions
{
    /// <summary>
    /// Gets the default single-worker async transport configuration with timeout disabled.
    /// </summary>
    public static RadarProcessingAsyncExecutionOptions Default { get; } = new();

    /// <summary>
    /// Creates async execution options and validates worker topology and timeout policy constraints.
    /// </summary>
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

    /// <summary>
    /// Gets the number of async workers available for shard dispatch.
    /// </summary>
    public int WorkerCount { get; }

    /// <summary>
    /// Gets the per-worker queue capacity used by transport adapters.
    /// </summary>
    public int QueueCapacity { get; }

    /// <summary>
    /// Gets how shard work is assigned to workers.
    /// </summary>
    public RadarProcessingWorkerAffinity WorkerAffinity { get; }

    /// <summary>
    /// Gets the policy applied when a batch exceeds <see cref="BatchTimeout"/>.
    /// </summary>
    public RadarProcessingWorkerTimeoutPolicy TimeoutPolicy { get; }

    /// <summary>
    /// Gets the positive timeout applied to one batch when timeout policy is enabled.
    /// </summary>
    public TimeSpan? BatchTimeout { get; }

    /// <summary>
    /// Gets whether batch timeout enforcement is configured.
    /// </summary>
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
