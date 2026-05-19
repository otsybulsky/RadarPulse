using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncWorkerGroupResult
{
    public RadarProcessingAsyncWorkerGroupResult(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        RadarProcessingAsyncWorkerGroupError error = RadarProcessingAsyncWorkerGroupError.None)
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
    }

    public RadarProcessingWorkerGroupStatus Status { get; }

    public RadarProcessingAsyncBatchScopeResult? BatchResult { get; }

    public RadarProcessingAsyncWorkerGroupError Error { get; }

    public bool IsSuccess =>
        Error == RadarProcessingAsyncWorkerGroupError.None &&
        BatchResult?.IsSuccess == true;

    public bool IsRejected => Error != RadarProcessingAsyncWorkerGroupError.None;

    public static RadarProcessingAsyncWorkerGroupResult Completed(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncBatchScopeResult batchResult) =>
        new(status, batchResult);

    public static RadarProcessingAsyncWorkerGroupResult Rejected(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncWorkerGroupError error,
        RadarProcessingAsyncBatchScopeResult? batchResult = null)
    {
        if (error == RadarProcessingAsyncWorkerGroupError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Rejected result requires an explicit error.");
        }

        return new RadarProcessingAsyncWorkerGroupResult(status, batchResult, error);
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
            not RadarProcessingAsyncWorkerGroupError.EnqueueRejected)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}
