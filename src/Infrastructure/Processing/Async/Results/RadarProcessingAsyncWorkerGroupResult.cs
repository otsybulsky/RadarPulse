using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result of dispatching a batch scope through the async worker group.
/// </summary>
/// <remarks>
/// Successful results must carry a batch scope result. Rejected results carry an
/// explicit worker group error, optional partial batch evidence, drain evidence,
/// and inferred failure/cancellation classifications for telemetry.
/// </remarks>
public sealed record RadarProcessingAsyncWorkerGroupResult
{
    /// <summary>
    /// Creates a worker group result and validates consistency between status and evidence.
    /// </summary>
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

    /// <summary>
    /// Worker group lifecycle status captured for the dispatch.
    /// </summary>
    public RadarProcessingWorkerGroupStatus Status { get; }

    /// <summary>
    /// Batch scope result when work reached the completion barrier.
    /// </summary>
    public RadarProcessingAsyncBatchScopeResult? BatchResult { get; }

    /// <summary>
    /// Worker group dispatch error, or none for a completed dispatch.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupError Error { get; }

    /// <summary>
    /// Drain and outstanding-work evidence captured around dispatch completion.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupDrainResult DrainResult { get; }

    /// <summary>
    /// Failure classification inferred or supplied for telemetry.
    /// </summary>
    public RadarProcessingAsyncFailureKind FailureKind { get; }

    /// <summary>
    /// Cancellation classification inferred or supplied for telemetry.
    /// </summary>
    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    /// <summary>
    /// Timeout details when timeout policy fired.
    /// </summary>
    public RadarProcessingAsyncTimeoutResult TimeoutResult { get; }

    /// <summary>
    /// Worker group health transition caused by timeout or fault.
    /// </summary>
    public RadarProcessingWorkerGroupHealthTransition? HealthTransition { get; }

    /// <summary>
    /// Indicates whether the worker group accepted and completed the batch successfully.
    /// </summary>
    public bool IsSuccess =>
        Error == RadarProcessingAsyncWorkerGroupError.None &&
        BatchResult?.IsSuccess == true;

    /// <summary>
    /// Indicates whether dispatch was rejected before a successful batch completion.
    /// </summary>
    public bool IsRejected => Error != RadarProcessingAsyncWorkerGroupError.None;

    /// <summary>
    /// Creates a completed worker group result.
    /// </summary>
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

    /// <summary>
    /// Creates a rejected worker group result with explicit rejection evidence.
    /// </summary>
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
