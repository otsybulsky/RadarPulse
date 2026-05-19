namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedPayloadOptions
{
    public static RadarProcessingRetainedPayloadOptions Default { get; } = new();

    public RadarProcessingRetainedPayloadOptions(
        RadarProcessingRetainedPayloadStrategy strategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
        long? maxRetainedPayloadBytes = null)
    {
        EnsureKnownStrategy(strategy);
        if (maxRetainedPayloadBytes.HasValue &&
            maxRetainedPayloadBytes.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetainedPayloadBytes),
                maxRetainedPayloadBytes,
                "Maximum retained payload bytes must be positive when specified.");
        }

        Strategy = strategy;
        MaxRetainedPayloadBytes = maxRetainedPayloadBytes;
    }

    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    public long? MaxRetainedPayloadBytes { get; }

    public bool HasMaxRetainedPayloadBytes => MaxRetainedPayloadBytes.HasValue;

    internal static void EnsureKnownStrategy(
        RadarProcessingRetainedPayloadStrategy strategy)
    {
        if (strategy is not RadarProcessingRetainedPayloadStrategy.SnapshotCopy and
            not RadarProcessingRetainedPayloadStrategy.PooledCopy and
            not RadarProcessingRetainedPayloadStrategy.BuilderTransfer)
        {
            throw new ArgumentOutOfRangeException(nameof(strategy));
        }
    }
}
