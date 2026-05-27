namespace RadarPulse.Domain.Processing;

/// <summary>
/// Controls how worker health is updated when async batch processing exceeds a timeout.
/// </summary>
public enum RadarProcessingWorkerTimeoutPolicy : byte
{
    /// <summary>
    /// Disables timeout enforcement.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Marks the worker group unhealthy when timeout is observed.
    /// </summary>
    MarkUnhealthy = 1,

    /// <summary>
    /// Requests cancellation and marks the worker group unhealthy when timeout is observed.
    /// </summary>
    RequestCancellationAndMarkUnhealthy = 2
}
