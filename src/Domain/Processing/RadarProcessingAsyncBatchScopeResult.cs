namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingAsyncBatchScopeResult
{
    public RadarProcessingAsyncBatchScopeResult(
        RadarProcessingAsyncBatchCompletion completion,
        RadarProcessingAsyncBatchCompletionError error = RadarProcessingAsyncBatchCompletionError.None)
    {
        ArgumentNullException.ThrowIfNull(completion);
        EnsureKnownError(error);

        Completion = completion;
        Error = error;
    }

    public RadarProcessingAsyncBatchCompletion Completion { get; }

    public RadarProcessingAsyncBatchCompletionError Error { get; }

    public bool IsSuccess => Error == RadarProcessingAsyncBatchCompletionError.None;

    internal static void EnsureKnownError(
        RadarProcessingAsyncBatchCompletionError error)
    {
        if (error is not RadarProcessingAsyncBatchCompletionError.None and
            not RadarProcessingAsyncBatchCompletionError.ScopeMismatch and
            not RadarProcessingAsyncBatchCompletionError.TopologyVersionMismatch and
            not RadarProcessingAsyncBatchCompletionError.WorkItemOutOfRange and
            not RadarProcessingAsyncBatchCompletionError.DuplicateCompletion and
            not RadarProcessingAsyncBatchCompletionError.MissingCompletion and
            not RadarProcessingAsyncBatchCompletionError.ScopeClosed and
            not RadarProcessingAsyncBatchCompletionError.WorkFailed and
            not RadarProcessingAsyncBatchCompletionError.WorkCanceled)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}
