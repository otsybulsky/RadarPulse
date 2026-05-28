namespace RadarPulse.Domain.Processing;

/// <summary>
/// Options that select how queued payloads are retained.
/// </summary>
public sealed record RadarProcessingRetainedPayloadOptions
{
    /// <summary>
    /// Default retained payload configuration.
    /// </summary>
    public static RadarProcessingRetainedPayloadOptions Default { get; } = new();

    /// <summary>
    /// Creates retained payload options.
    /// </summary>
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

    /// <summary>
    /// Strategy used to retain queued payload ownership.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    /// <summary>
    /// Optional maximum retained payload bytes allowed by the caller.
    /// </summary>
    public long? MaxRetainedPayloadBytes { get; }

    /// <summary>
    /// Indicates whether a retained payload byte limit is configured.
    /// </summary>
    public bool HasMaxRetainedPayloadBytes => MaxRetainedPayloadBytes.HasValue;

    /// <summary>
    /// Throws when a retained payload strategy is outside the known contract.
    /// </summary>
    public static void EnsureKnownStrategy(
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
