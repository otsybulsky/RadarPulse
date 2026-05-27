namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports the outcome of a worker group lifecycle operation.
/// </summary>
public sealed record RadarProcessingWorkerLifecycleResult
{
    /// <summary>
    /// Creates a lifecycle result and validates that failure status carries the same error.
    /// </summary>
    public RadarProcessingWorkerLifecycleResult(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingWorkerLifecycleError error = RadarProcessingWorkerLifecycleError.None)
    {
        ArgumentNullException.ThrowIfNull(status);
        RadarProcessingWorkerGroupStatus.EnsureKnownLifecycleError(error);

        if (error != RadarProcessingWorkerLifecycleError.None &&
            status.LastError != error)
        {
            throw new ArgumentException("Failure status last error must match result error.", nameof(status));
        }

        Status = status;
        Error = error;
    }

    /// <summary>
    /// Gets the worker group status after the lifecycle operation.
    /// </summary>
    public RadarProcessingWorkerGroupStatus Status { get; }

    /// <summary>
    /// Gets the lifecycle error, or <see cref="RadarProcessingWorkerLifecycleError.None"/>.
    /// </summary>
    public RadarProcessingWorkerLifecycleError Error { get; }

    /// <summary>
    /// Gets whether the lifecycle operation succeeded.
    /// </summary>
    public bool IsSuccess => Error == RadarProcessingWorkerLifecycleError.None;

    /// <summary>
    /// Creates a successful lifecycle result.
    /// </summary>
    public static RadarProcessingWorkerLifecycleResult Succeeded(
        RadarProcessingWorkerGroupStatus status) =>
        new(status);

    /// <summary>
    /// Creates a failed lifecycle result with an explicit error.
    /// </summary>
    public static RadarProcessingWorkerLifecycleResult Failed(
        RadarProcessingWorkerLifecycleError error,
        RadarProcessingWorkerGroupStatus status)
    {
        if (error == RadarProcessingWorkerLifecycleError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Failure result requires an explicit error.");
        }

        return new RadarProcessingWorkerLifecycleResult(status, error);
    }
}
