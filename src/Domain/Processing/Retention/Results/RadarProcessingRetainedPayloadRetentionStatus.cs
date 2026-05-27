namespace RadarPulse.Domain.Processing;

/// <summary>
/// Outcome of retaining payload ownership for queued processing.
/// </summary>
public enum RadarProcessingRetainedPayloadRetentionStatus
{
    /// <summary>
    /// Retention succeeded and produced an owned batch.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// The requested retention strategy is not supported by the implementation.
    /// </summary>
    UnsupportedStrategy = 2,

    /// <summary>
    /// Copying payload data failed.
    /// </summary>
    FailedCopy = 3,

    /// <summary>
    /// Retention was canceled before completion.
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// The input batch or retention request was invalid.
    /// </summary>
    InvalidInput = 5
}
