namespace RadarPulse.Domain.Processing;

/// <summary>
/// Describes where cancellation was observed in async batch processing.
/// </summary>
public enum RadarProcessingAsyncCancellationKind : byte
{
    /// <summary>
    /// No cancellation was observed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Cancellation was requested before dispatch accepted the work.
    /// </summary>
    BeforeDispatch = 1,

    /// <summary>
    /// Cancellation was observed while the work item waited in a queue.
    /// </summary>
    WhileQueued = 2,

    /// <summary>
    /// Cancellation was observed while the worker was running the item.
    /// </summary>
    WhileRunning = 3,

    /// <summary>
    /// Cancellation was caused by timeout enforcement.
    /// </summary>
    Timeout = 4,

    /// <summary>
    /// Multiple cancellation locations were observed in one batch.
    /// </summary>
    Mixed = 5
}
