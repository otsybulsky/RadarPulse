namespace RadarPulse.Domain.Processing;

/// <summary>
/// Ownership state for a retained payload resource.
/// </summary>
public enum RadarProcessingRetainedBatchResourceState
{
    /// <summary>
    /// The producer/provider still owns the resource.
    /// </summary>
    ProviderOwned = 1,

    /// <summary>
    /// The queue owns the resource while the batch is pending.
    /// </summary>
    QueueOwned = 2,

    /// <summary>
    /// A consumer lease owns the resource.
    /// </summary>
    ConsumerOwned = 3,

    /// <summary>
    /// The resource was released successfully or release was not required.
    /// </summary>
    Released = 4,

    /// <summary>
    /// Resource release failed and should not be retried implicitly.
    /// </summary>
    ReleaseFailed = 5
}
