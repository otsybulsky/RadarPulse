using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncWorkerGroupResult
{
    public RadarProcessingAsyncWorkerGroupResult(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        RadarProcessingAsyncWorkerGroupError error = RadarProcessingAsyncWorkerGroupError.None,
        RadarProcessingAsyncWorkerGroupDrainResult? drainResult = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        EnsureKnownError(error);

        if (error == RadarProcessingAsyncWorkerGroupError.None &&
            batchResult is null)
        {
            throw new ArgumentException("Successful dispatch result requires a batch result.", nameof(batchResult));
        }

        Status = status;
        BatchResult = batchResult;
        Error = error;
        DrainResult = drainResult ?? new RadarProcessingAsyncWorkerGroupDrainResult();
    }

    public RadarProcessingWorkerGroupStatus Status { get; }

    public RadarProcessingAsyncBatchScopeResult? BatchResult { get; }

    public RadarProcessingAsyncWorkerGroupError Error { get; }

    public RadarProcessingAsyncWorkerGroupDrainResult DrainResult { get; }

    public bool IsSuccess =>
        Error == RadarProcessingAsyncWorkerGroupError.None &&
        BatchResult?.IsSuccess == true;

    public bool IsRejected => Error != RadarProcessingAsyncWorkerGroupError.None;

    public static RadarProcessingAsyncWorkerGroupResult Completed(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncBatchScopeResult batchResult,
        RadarProcessingAsyncWorkerGroupDrainResult drainResult) =>
        new(status, batchResult, drainResult: drainResult);

    public static RadarProcessingAsyncWorkerGroupResult Rejected(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncWorkerGroupError error,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        RadarProcessingAsyncWorkerGroupDrainResult? drainResult = null)
    {
        if (error == RadarProcessingAsyncWorkerGroupError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Rejected result requires an explicit error.");
        }

        return new RadarProcessingAsyncWorkerGroupResult(status, batchResult, error, drainResult);
    }

    internal static void EnsureKnownError(
        RadarProcessingAsyncWorkerGroupError error)
    {
        if (error is not RadarProcessingAsyncWorkerGroupError.None and
            not RadarProcessingAsyncWorkerGroupError.AlreadyStarted and
            not RadarProcessingAsyncWorkerGroupError.NotStarted and
            not RadarProcessingAsyncWorkerGroupError.NotRunning and
            not RadarProcessingAsyncWorkerGroupError.Stopping and
            not RadarProcessingAsyncWorkerGroupError.Stopped and
            not RadarProcessingAsyncWorkerGroupError.Faulted and
            not RadarProcessingAsyncWorkerGroupError.Disposed and
            not RadarProcessingAsyncWorkerGroupError.AlreadyInFlight and
            not RadarProcessingAsyncWorkerGroupError.EnqueueRejected and
            not RadarProcessingAsyncWorkerGroupError.TimedOut and
            not RadarProcessingAsyncWorkerGroupError.ScopeClosed)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}
