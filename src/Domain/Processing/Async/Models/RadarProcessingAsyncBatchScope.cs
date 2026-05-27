namespace RadarPulse.Domain.Processing;

/// <summary>
/// Collects async work completions for one batch and produces an aggregate completion snapshot.
/// </summary>
public sealed class RadarProcessingAsyncBatchScope
{
    private readonly RadarProcessingAsyncWorkCompletion?[] completions;
    private int recordedCompletionCount;
    private bool isClosed;

    /// <summary>
    /// Creates a batch scope with a fixed expected work item count.
    /// </summary>
    public RadarProcessingAsyncBatchScope(
        long batchSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int expectedWorkItemCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedWorkItemCount);

        BatchSequence = batchSequence;
        TopologyVersion = topologyVersion;
        ExpectedWorkItemCount = expectedWorkItemCount;
        completions = new RadarProcessingAsyncWorkCompletion[expectedWorkItemCount];
    }

    /// <summary>
    /// Gets the batch sequence guarded by the scope.
    /// </summary>
    public long BatchSequence { get; }

    /// <summary>
    /// Gets the topology version shared by all work items and completions.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Gets the exact number of work item completions required to close the scope.
    /// </summary>
    public int ExpectedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of unique completions recorded so far.
    /// </summary>
    public int RecordedCompletionCount => recordedCompletionCount;

    /// <summary>
    /// Gets whether the scope has been completed and closed to further completions.
    /// </summary>
    public bool IsClosed => isClosed;

    /// <summary>
    /// Creates a scoped work item with this batch sequence and topology version.
    /// </summary>
    public RadarProcessingAsyncWorkItem CreateWorkItem(
        int workItemId,
        RadarProcessingWorkerId workerId,
        int shardId,
        IReadOnlyCollection<int> partitionIds)
    {
        EnsureWorkItemId(workItemId);
        return new RadarProcessingAsyncWorkItem(
            BatchSequence,
            workItemId,
            TopologyVersion,
            workerId,
            shardId,
            partitionIds);
    }

    /// <summary>
    /// Records one completion when it matches the scope and has not already been recorded.
    /// </summary>
    public RadarProcessingAsyncBatchScopeResult RecordCompletion(
        RadarProcessingAsyncWorkCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        if (isClosed)
        {
            return Failed(RadarProcessingAsyncBatchCompletionError.ScopeClosed);
        }

        if (completion.BatchSequence != BatchSequence)
        {
            return Failed(RadarProcessingAsyncBatchCompletionError.ScopeMismatch);
        }

        if (completion.TopologyVersion != TopologyVersion)
        {
            return Failed(RadarProcessingAsyncBatchCompletionError.TopologyVersionMismatch);
        }

        if ((uint)completion.WorkItemId >= (uint)ExpectedWorkItemCount)
        {
            return Failed(RadarProcessingAsyncBatchCompletionError.WorkItemOutOfRange);
        }

        if (completions[completion.WorkItemId] is not null)
        {
            return Failed(RadarProcessingAsyncBatchCompletionError.DuplicateCompletion);
        }

        completions[completion.WorkItemId] = completion;
        recordedCompletionCount++;
        return Succeeded();
    }

    /// <summary>
    /// Closes the scope once all expected work items have completed.
    /// </summary>
    public RadarProcessingAsyncBatchScopeResult Complete()
    {
        if (recordedCompletionCount != ExpectedWorkItemCount)
        {
            return Failed(RadarProcessingAsyncBatchCompletionError.MissingCompletion);
        }

        isClosed = true;
        var completion = CreateSnapshot();
        if (completion.FailedWorkItemCount > 0)
        {
            return new RadarProcessingAsyncBatchScopeResult(
                completion,
                RadarProcessingAsyncBatchCompletionError.WorkFailed);
        }

        if (completion.CanceledWorkItemCount > 0)
        {
            return new RadarProcessingAsyncBatchScopeResult(
                completion,
                RadarProcessingAsyncBatchCompletionError.WorkCanceled);
        }

        return new RadarProcessingAsyncBatchScopeResult(completion);
    }

    /// <summary>
    /// Creates an immutable snapshot of currently recorded completions.
    /// </summary>
    public RadarProcessingAsyncBatchCompletion CreateSnapshot() =>
        new(
            BatchSequence,
            TopologyVersion,
            ExpectedWorkItemCount,
            CopyRecordedCompletions(),
            isClosed);

    private RadarProcessingAsyncBatchScopeResult Succeeded() =>
        new(CreateSnapshot());

    private RadarProcessingAsyncBatchScopeResult Failed(
        RadarProcessingAsyncBatchCompletionError error) =>
        new(CreateSnapshot(), error);

    private IReadOnlyCollection<RadarProcessingAsyncWorkCompletion> CopyRecordedCompletions()
    {
        if (recordedCompletionCount == 0)
        {
            return Array.Empty<RadarProcessingAsyncWorkCompletion>();
        }

        var result = new RadarProcessingAsyncWorkCompletion[recordedCompletionCount];
        var index = 0;
        foreach (var completion in completions)
        {
            if (completion is not null)
            {
                result[index++] = completion;
            }
        }

        return result;
    }

    private void EnsureWorkItemId(int workItemId)
    {
        if ((uint)workItemId >= (uint)ExpectedWorkItemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(workItemId));
        }
    }
}
