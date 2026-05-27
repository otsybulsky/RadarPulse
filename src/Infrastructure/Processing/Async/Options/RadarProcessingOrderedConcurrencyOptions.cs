namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Bounded concurrency settings for ordered concurrent queue drains.
/// </summary>
public sealed record RadarProcessingOrderedConcurrencyOptions
{
    /// <summary>
    /// Default number of active batches allowed ahead of ordered publish.
    /// </summary>
    public const int DefaultActiveBatchCapacity = 4;

    /// <summary>
    /// Default ordered concurrent drain settings.
    /// </summary>
    public static RadarProcessingOrderedConcurrencyOptions Default { get; } =
        new(DefaultActiveBatchCapacity);

    /// <summary>
    /// Settings that force sequential processing.
    /// </summary>
    public static RadarProcessingOrderedConcurrencyOptions Sequential { get; } =
        new(1);

    /// <summary>
    /// Creates ordered concurrency settings with a positive active batch capacity.
    /// </summary>
    public RadarProcessingOrderedConcurrencyOptions(int activeBatchCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activeBatchCapacity);

        ActiveBatchCapacity = activeBatchCapacity;
    }

    /// <summary>
    /// Maximum number of batches that may be computing before ordered publication catches up.
    /// </summary>
    public int ActiveBatchCapacity { get; }

    /// <summary>
    /// Indicates whether the configured capacity is equivalent to sequential processing.
    /// </summary>
    public bool IsSequential => ActiveBatchCapacity == 1;
}
