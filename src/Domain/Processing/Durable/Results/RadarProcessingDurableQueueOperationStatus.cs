namespace RadarPulse.Domain.Processing;

/// <summary>
/// Outcome of one durable queue operation.
/// </summary>
public enum RadarProcessingDurableQueueOperationStatus
{
    /// <summary>
    /// A new envelope was accepted.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The requested accept operation matched an existing envelope.
    /// </summary>
    Duplicate = 2,

    /// <summary>
    /// An envelope was claimed for processing.
    /// </summary>
    Claimed = 3,

    /// <summary>
    /// No eligible envelope was available.
    /// </summary>
    Empty = 4,

    /// <summary>
    /// An envelope was marked completed.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// An envelope was marked failed.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// A claimed attempt was abandoned.
    /// </summary>
    Abandoned = 7,

    /// <summary>
    /// An envelope was moved back for retry.
    /// </summary>
    Retried = 8,

    /// <summary>
    /// An envelope was marked poison.
    /// </summary>
    Poisoned = 9,

    /// <summary>
    /// An envelope output was committed.
    /// </summary>
    Committed = 10,

    /// <summary>
    /// Retained resources were released for an envelope.
    /// </summary>
    Released = 11,

    /// <summary>
    /// An envelope was canceled.
    /// </summary>
    Canceled = 12,

    /// <summary>
    /// The target envelope was not found.
    /// </summary>
    NotFound = 13,

    /// <summary>
    /// The operation was invalid for the envelope's current state.
    /// </summary>
    InvalidState = 14
}
