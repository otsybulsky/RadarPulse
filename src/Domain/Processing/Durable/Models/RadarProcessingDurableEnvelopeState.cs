namespace RadarPulse.Domain.Processing;

/// <summary>
/// Durable envelope lifecycle state.
/// </summary>
/// <remarks>
/// The state machine separates acceptance, claiming, processing completion,
/// commit, failure handling, cancellation, and retained-resource release so
/// recovery can reason about incomplete work deterministically.
/// </remarks>
public enum RadarProcessingDurableEnvelopeState
{
    /// <summary>
    /// The envelope is accepted and waiting to be claimed.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// A worker has claimed the envelope for processing.
    /// </summary>
    Claimed = 2,

    /// <summary>
    /// Processing completed but the result has not been committed.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Processing output has been committed.
    /// </summary>
    Committed = 4,

    /// <summary>
    /// Processing failed and may be retryable depending on policy.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// The envelope is not safe to retry automatically.
    /// </summary>
    Poison = 6,

    /// <summary>
    /// A claimed attempt was abandoned and may be retryable depending on policy.
    /// </summary>
    Abandoned = 7,

    /// <summary>
    /// The envelope was canceled before normal completion.
    /// </summary>
    Canceled = 8,

    /// <summary>
    /// Retained resources tied to a terminal envelope were released.
    /// </summary>
    Released = 9
}
