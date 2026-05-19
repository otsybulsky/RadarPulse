namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingWorkerLifecycleResult
{
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

    public RadarProcessingWorkerGroupStatus Status { get; }

    public RadarProcessingWorkerLifecycleError Error { get; }

    public bool IsSuccess => Error == RadarProcessingWorkerLifecycleError.None;

    public static RadarProcessingWorkerLifecycleResult Succeeded(
        RadarProcessingWorkerGroupStatus status) =>
        new(status);

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
