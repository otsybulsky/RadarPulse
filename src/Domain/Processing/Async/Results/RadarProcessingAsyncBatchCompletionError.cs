namespace RadarPulse.Domain.Processing;

/// <summary>
/// Identifies why an async batch scope operation failed.
/// </summary>
public enum RadarProcessingAsyncBatchCompletionError : byte
{
    /// <summary>
    /// No scope error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// A completion belonged to a different batch sequence.
    /// </summary>
    ScopeMismatch = 1,

    /// <summary>
    /// A completion belonged to a different topology version.
    /// </summary>
    TopologyVersionMismatch = 2,

    /// <summary>
    /// A completion referenced a work item id outside the expected range.
    /// </summary>
    WorkItemOutOfRange = 3,

    /// <summary>
    /// A work item completion was recorded more than once.
    /// </summary>
    DuplicateCompletion = 4,

    /// <summary>
    /// The scope was closed before every expected completion arrived.
    /// </summary>
    MissingCompletion = 5,

    /// <summary>
    /// A completion was submitted after the scope had closed.
    /// </summary>
    ScopeClosed = 6,

    /// <summary>
    /// At least one work item failed.
    /// </summary>
    WorkFailed = 7,

    /// <summary>
    /// At least one work item was canceled.
    /// </summary>
    WorkCanceled = 8
}
