namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports the result of recording or closing an async batch scope.
/// </summary>
public sealed record RadarProcessingAsyncBatchScopeResult
{
    /// <summary>
    /// Creates a scope result with the current aggregate snapshot and completion error.
    /// </summary>
    public RadarProcessingAsyncBatchScopeResult(
        RadarProcessingAsyncBatchCompletion completion,
        RadarProcessingAsyncBatchCompletionError error = RadarProcessingAsyncBatchCompletionError.None)
    {
        ArgumentNullException.ThrowIfNull(completion);
        EnsureKnownError(error);

        Completion = completion;
        Error = error;
    }

    /// <summary>
    /// Gets the aggregate completion snapshot at the time of the operation.
    /// </summary>
    public RadarProcessingAsyncBatchCompletion Completion { get; }

    /// <summary>
    /// Gets the scope error, or <see cref="RadarProcessingAsyncBatchCompletionError.None"/>.
    /// </summary>
    public RadarProcessingAsyncBatchCompletionError Error { get; }

    /// <summary>
    /// Gets whether the scope operation succeeded.
    /// </summary>
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
