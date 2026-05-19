namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingAsyncBatchScope
{
    private readonly RadarProcessingAsyncWorkCompletion?[] completions;
    private int recordedCompletionCount;
    private bool isClosed;

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

    public long BatchSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public int ExpectedWorkItemCount { get; }

    public int RecordedCompletionCount => recordedCompletionCount;

    public bool IsClosed => isClosed;

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
