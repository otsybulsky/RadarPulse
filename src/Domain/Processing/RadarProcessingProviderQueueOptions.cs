namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingProviderQueueOptions
{
    public static RadarProcessingProviderQueueOptions Default { get; } = new();

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

    public int Capacity { get; }

    public RadarProcessingProviderQueueFullMode FullMode { get; }

    public TimeSpan? EnqueueTimeout { get; }

    public bool HasEnqueueTimeout => EnqueueTimeout.HasValue;

    public RadarProcessingProviderQueueShutdownMode ShutdownMode { get; }

    public int RecentDetailCapacity { get; }

    public long? MaxRetainedPayloadBytes { get; }

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
