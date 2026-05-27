namespace RadarPulse.Domain.Processing;

/// <summary>
/// Configuration for the owned provider queue between archive intake and processing.
/// </summary>
/// <remarks>
/// The options define capacity, backpressure behavior, shutdown behavior, recent
/// telemetry retention, and optional retained-payload pressure bounds. They apply
/// to in-process queued-provider contours and do not imply an external broker.
/// </remarks>
public sealed record RadarProcessingProviderQueueOptions
{
    /// <summary>
    /// Accepted default queue configuration.
    /// </summary>
    public static RadarProcessingProviderQueueOptions Default { get; } = new();

    /// <summary>
    /// Creates provider queue options with validated capacity and pressure settings.
    /// </summary>
    public RadarProcessingProviderQueueOptions(
        int capacity = 1,
        RadarProcessingProviderQueueFullMode fullMode = RadarProcessingProviderQueueFullMode.Wait,
        TimeSpan? enqueueTimeout = null,
        RadarProcessingProviderQueueShutdownMode shutdownMode = RadarProcessingProviderQueueShutdownMode.Drain,
        int recentDetailCapacity = 16,
        long? maxRetainedPayloadBytes = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(recentDetailCapacity);
        EnsureKnownFullMode(fullMode);
        EnsureKnownShutdownMode(shutdownMode);
        ValidateEnqueueTimeout(fullMode, enqueueTimeout);
        ValidateMaxRetainedPayloadBytes(maxRetainedPayloadBytes);

        Capacity = capacity;
        FullMode = fullMode;
        EnqueueTimeout = enqueueTimeout;
        ShutdownMode = shutdownMode;
        RecentDetailCapacity = recentDetailCapacity;
        MaxRetainedPayloadBytes = maxRetainedPayloadBytes;
    }

    /// <summary>
    /// Maximum number of owned batches retained by the queue.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Behavior when enqueue is attempted at capacity.
    /// </summary>
    public RadarProcessingProviderQueueFullMode FullMode { get; }

    /// <summary>
    /// Optional wait bound used only by wait-on-full mode.
    /// </summary>
    public TimeSpan? EnqueueTimeout { get; }

    /// <summary>
    /// Indicates whether enqueue waits have an explicit timeout.
    /// </summary>
    public bool HasEnqueueTimeout => EnqueueTimeout.HasValue;

    /// <summary>
    /// Behavior for accepted work during shutdown.
    /// </summary>
    public RadarProcessingProviderQueueShutdownMode ShutdownMode { get; }

    /// <summary>
    /// Number of recent queue details retained for diagnostics.
    /// </summary>
    public int RecentDetailCapacity { get; }

    /// <summary>
    /// Optional retained payload budget across queued provider resources.
    /// </summary>
    public long? MaxRetainedPayloadBytes { get; }

    /// <summary>
    /// Indicates whether retained payload pressure is budgeted.
    /// </summary>
    public bool HasMaxRetainedPayloadBytes => MaxRetainedPayloadBytes.HasValue;

    internal static void EnsureKnownFullMode(
        RadarProcessingProviderQueueFullMode fullMode)
    {
        if (fullMode is not RadarProcessingProviderQueueFullMode.ReturnFull and
            not RadarProcessingProviderQueueFullMode.Wait)
        {
            throw new ArgumentOutOfRangeException(nameof(fullMode));
        }
    }

    internal static void EnsureKnownShutdownMode(
        RadarProcessingProviderQueueShutdownMode shutdownMode)
    {
        if (shutdownMode is not RadarProcessingProviderQueueShutdownMode.Drain and
            not RadarProcessingProviderQueueShutdownMode.CancelQueued)
        {
            throw new ArgumentOutOfRangeException(nameof(shutdownMode));
        }
    }

    private static void ValidateEnqueueTimeout(
        RadarProcessingProviderQueueFullMode fullMode,
        TimeSpan? enqueueTimeout)
    {
        if (!enqueueTimeout.HasValue)
        {
            return;
        }

        if (enqueueTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enqueueTimeout),
                enqueueTimeout,
                "Enqueue timeout must be positive when specified.");
        }

        if (fullMode != RadarProcessingProviderQueueFullMode.Wait)
        {
            throw new ArgumentException(
                "Enqueue timeout requires wait-on-full queue mode.",
                nameof(enqueueTimeout));
        }
    }

    private static void ValidateMaxRetainedPayloadBytes(
        long? maxRetainedPayloadBytes)
    {
        if (!maxRetainedPayloadBytes.HasValue)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRetainedPayloadBytes.Value);
    }
}
