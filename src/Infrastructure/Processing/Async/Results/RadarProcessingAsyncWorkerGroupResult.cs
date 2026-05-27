using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncWorkerGroupResult
{
    public RadarProcessingAsyncWorkerGroupResult(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        RadarProcessingAsyncWorkerGroupError error = RadarProcessingAsyncWorkerGroupError.None,
        RadarProcessingAsyncWorkerGroupDrainResult? drainResult = null,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None,
        RadarProcessingAsyncTimeoutResult? timeoutResult = null,
        RadarProcessingWorkerGroupHealthTransition? healthTransition = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        EnsureKnownError(error);
        EnsureKnownFailureKind(failureKind);
        EnsureKnownCancellationKind(cancellationKind);

        if (error == RadarProcessingAsyncWorkerGroupError.None &&
            batchResult is null)
        {
            throw new ArgumentException("Successful dispatch result requires a batch result.", nameof(batchResult));
        }

        var effectiveTimeoutResult = timeoutResult ?? RadarProcessingAsyncTimeoutResult.None;
        if (error == RadarProcessingAsyncWorkerGroupError.TimedOut &&
            !effectiveTimeoutResult.TimedOut)
        {
            throw new ArgumentException("Timed-out dispatch result requires timeout details.", nameof(timeoutResult));
        }

        if (error != RadarProcessingAsyncWorkerGroupError.TimedOut &&
            effectiveTimeoutResult.TimedOut)
        {
            throw new ArgumentException("Only timed-out dispatch result can carry timeout details.", nameof(timeoutResult));
        }

        if (healthTransition is not null &&
            error is not RadarProcessingAsyncWorkerGroupError.TimedOut and
                not RadarProcessingAsyncWorkerGroupError.Faulted)
        {
            throw new ArgumentException(
                "Health transition can be attached only to faulted or timed-out dispatch results.",
                nameof(healthTransition));
        }

        Status = status;
        BatchResult = batchResult;
        Error = error;
        DrainResult = drainResult ?? new RadarProcessingAsyncWorkerGroupDrainResult();
        TimeoutResult = effectiveTimeoutResult;
        HealthTransition = healthTransition;
        FailureKind = failureKind == RadarProcessingAsyncFailureKind.None
            ? InferFailureKind(error, batchResult)
            : failureKind;
        CancellationKind = cancellationKind == RadarProcessingAsyncCancellationKind.None
            ? InferCancellationKind(batchResult)
            : cancellationKind;
    }

    public RadarProcessingWorkerGroupStatus Status { get; }

    public RadarProcessingAsyncBatchScopeResult? BatchResult { get; }

    public RadarProcessingAsyncWorkerGroupError Error { get; }

    public RadarProcessingAsyncWorkerGroupDrainResult DrainResult { get; }

    public RadarProcessingAsyncFailureKind FailureKind { get; }

    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    public RadarProcessingAsyncTimeoutResult TimeoutResult { get; }

    public RadarProcessingWorkerGroupHealthTransition? HealthTransition { get; }

    public bool IsSuccess =>
        Error == RadarProcessingAsyncWorkerGroupError.None &&
        BatchResult?.IsSuccess == true;

    public bool IsRejected => Error != RadarProcessingAsyncWorkerGroupError.None;

    public static RadarProcessingAsyncWorkerGroupResult Completed(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncBatchScopeResult batchResult,
        RadarProcessingAsyncWorkerGroupDrainResult drainResult,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None) =>
        new(
            status,
            batchResult,
            drainResult: drainResult,
            failureKind: failureKind,
            cancellationKind: cancellationKind);

    public static RadarProcessingAsyncWorkerGroupResult Rejected(
        RadarProcessingWorkerGroupStatus status,
        RadarProcessingAsyncWorkerGroupError error,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        RadarProcessingAsyncWorkerGroupDrainResult? drainResult = null,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None,
        RadarProcessingAsyncTimeoutResult? timeoutResult = null,
        RadarProcessingWorkerGroupHealthTransition? healthTransition = null)
    {
        if (error == RadarProcessingAsyncWorkerGroupError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Rejected result requires an explicit error.");
        }

        return new RadarProcessingAsyncWorkerGroupResult(
            status,
            batchResult,
            error,
            drainResult,
            failureKind,
            cancellationKind,
            timeoutResult,
            healthTransition);
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

    private static RadarProcessingAsyncFailureKind InferFailureKind(
        RadarProcessingAsyncWorkerGroupError error,
        RadarProcessingAsyncBatchScopeResult? batchResult) =>
        error switch
        {
            RadarProcessingAsyncWorkerGroupError.None => InferBatchFailureKind(batchResult),
            RadarProcessingAsyncWorkerGroupError.EnqueueRejected => RadarProcessingAsyncFailureKind.EnqueueRejected,
            RadarProcessingAsyncWorkerGroupError.TimedOut => RadarProcessingAsyncFailureKind.TimedOut,
            RadarProcessingAsyncWorkerGroupError.Faulted => RadarProcessingAsyncFailureKind.WorkerGroupFaulted,
            _ => RadarProcessingAsyncFailureKind.DispatchRejected
        };

    private static RadarProcessingAsyncFailureKind InferBatchFailureKind(
        RadarProcessingAsyncBatchScopeResult? batchResult)
    {
        if (batchResult?.Error != RadarProcessingAsyncBatchCompletionError.WorkFailed)
        {
            return RadarProcessingAsyncFailureKind.None;
        }

        foreach (var completion in batchResult.Completion.Completions)
        {
            if (completion.IsFailed)
            {
                return completion.FailureKind;
            }
        }

        return RadarProcessingAsyncFailureKind.WorkerReportedFailure;
    }

    private static RadarProcessingAsyncCancellationKind InferCancellationKind(
        RadarProcessingAsyncBatchScopeResult? batchResult)
    {
        if (batchResult?.Error != RadarProcessingAsyncBatchCompletionError.WorkCanceled)
        {
            return RadarProcessingAsyncCancellationKind.None;
        }

        var result = RadarProcessingAsyncCancellationKind.None;
        foreach (var completion in batchResult.Completion.Completions)
        {
            if (!completion.IsCanceled)
            {
                continue;
            }

            if (result == RadarProcessingAsyncCancellationKind.None)
            {
                result = completion.CancellationKind;
                continue;
            }

            if (result != completion.CancellationKind)
            {
                return RadarProcessingAsyncCancellationKind.Mixed;
            }
        }

        return result;
    }

    private static void EnsureKnownFailureKind(
        RadarProcessingAsyncFailureKind failureKind)
    {
        if (failureKind is not RadarProcessingAsyncFailureKind.None and
            not RadarProcessingAsyncFailureKind.WorkerReportedFailure and
            not RadarProcessingAsyncFailureKind.WorkerException and
            not RadarProcessingAsyncFailureKind.DispatchRejected and
            not RadarProcessingAsyncFailureKind.EnqueueRejected and
            not RadarProcessingAsyncFailureKind.TimedOut and
            not RadarProcessingAsyncFailureKind.WorkerGroupFaulted)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }
    }

    private static void EnsureKnownCancellationKind(
        RadarProcessingAsyncCancellationKind cancellationKind)
    {
        if (cancellationKind is not RadarProcessingAsyncCancellationKind.None and
            not RadarProcessingAsyncCancellationKind.BeforeDispatch and
            not RadarProcessingAsyncCancellationKind.WhileQueued and
            not RadarProcessingAsyncCancellationKind.WhileRunning and
            not RadarProcessingAsyncCancellationKind.Timeout and
            not RadarProcessingAsyncCancellationKind.Mixed)
        {
            throw new ArgumentOutOfRangeException(nameof(cancellationKind));
        }
    }
}
