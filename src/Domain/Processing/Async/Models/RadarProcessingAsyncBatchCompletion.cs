namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingAsyncBatchCompletion
{
    private readonly IReadOnlyList<RadarProcessingAsyncWorkCompletion> completions;

    public RadarProcessingAsyncBatchCompletion(
        long batchSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int expectedWorkItemCount,
        IReadOnlyCollection<RadarProcessingAsyncWorkCompletion>? completions = null,
        bool isClosed = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedWorkItemCount);

        BatchSequence = batchSequence;
        TopologyVersion = topologyVersion;
        ExpectedWorkItemCount = expectedWorkItemCount;
        this.completions = CopyCompletions(
            batchSequence,
            topologyVersion,
            expectedWorkItemCount,
            completions ?? Array.Empty<RadarProcessingAsyncWorkCompletion>(),
            out var succeededCount,
            out var failedCount,
            out var canceledCount,
            out var queueWaitTime,
            out var executionTime,
            out var processedStreamEventCount,
            out var processedPayloadValueCount);
        SucceededWorkItemCount = succeededCount;
        FailedWorkItemCount = failedCount;
        CanceledWorkItemCount = canceledCount;
        TotalQueueWaitTime = queueWaitTime;
        TotalExecutionTime = executionTime;
        ProcessedStreamEventCount = processedStreamEventCount;
        ProcessedPayloadValueCount = processedPayloadValueCount;
        IsClosed = isClosed;
    }

    public long BatchSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public int ExpectedWorkItemCount { get; }

    public int RecordedWorkItemCount => completions.Count;

    public int SucceededWorkItemCount { get; }

    public int FailedWorkItemCount { get; }

    public int CanceledWorkItemCount { get; }

    public TimeSpan TotalQueueWaitTime { get; }

    public TimeSpan TotalExecutionTime { get; }

    public long ProcessedStreamEventCount { get; }

    public long ProcessedPayloadValueCount { get; }

    public bool IsClosed { get; }

    public bool IsComplete => RecordedWorkItemCount == ExpectedWorkItemCount;

    public bool IsSuccessful =>
        IsComplete &&
        FailedWorkItemCount == 0 &&
        CanceledWorkItemCount == 0;

    public IReadOnlyList<RadarProcessingAsyncWorkCompletion> Completions => completions;

    private static IReadOnlyList<RadarProcessingAsyncWorkCompletion> CopyCompletions(
        long batchSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int expectedWorkItemCount,
        IReadOnlyCollection<RadarProcessingAsyncWorkCompletion> completions,
        out int succeededCount,
        out int failedCount,
        out int canceledCount,
        out TimeSpan totalQueueWaitTime,
        out TimeSpan totalExecutionTime,
        out long processedStreamEventCount,
        out long processedPayloadValueCount)
    {
        ArgumentNullException.ThrowIfNull(completions);

        var result = new RadarProcessingAsyncWorkCompletion[completions.Count];
        var seen = new bool[expectedWorkItemCount];
        succeededCount = 0;
        failedCount = 0;
        canceledCount = 0;
        totalQueueWaitTime = TimeSpan.Zero;
        totalExecutionTime = TimeSpan.Zero;
        processedStreamEventCount = 0;
        processedPayloadValueCount = 0;
        var index = 0;

        foreach (var completion in completions)
        {
            ArgumentNullException.ThrowIfNull(completion);
            if (completion.BatchSequence != batchSequence)
            {
                throw new ArgumentException("Completion batch sequence must match batch completion.", nameof(completions));
            }

            if (completion.TopologyVersion != topologyVersion)
            {
                throw new ArgumentException("Completion topology version must match batch completion.", nameof(completions));
            }

            if ((uint)completion.WorkItemId >= (uint)expectedWorkItemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(completions));
            }

            if (seen[completion.WorkItemId])
            {
                throw new ArgumentException("Completion work item ids must be unique.", nameof(completions));
            }

            seen[completion.WorkItemId] = true;
            result[index++] = completion;
            switch (completion.Status)
            {
                case RadarProcessingAsyncWorkStatus.Succeeded:
                    succeededCount++;
                    break;
                case RadarProcessingAsyncWorkStatus.Failed:
                    failedCount++;
                    break;
                case RadarProcessingAsyncWorkStatus.Canceled:
                    canceledCount++;
                    break;
                default:
                    RadarProcessingAsyncWorkCompletion.EnsureKnownStatus(completion.Status);
                    break;
            }

            totalQueueWaitTime += completion.QueueWaitTime;
            totalExecutionTime += completion.ExecutionTime;
            processedStreamEventCount = checked(processedStreamEventCount + completion.ProcessedStreamEventCount);
            processedPayloadValueCount = checked(processedPayloadValueCount + completion.ProcessedPayloadValueCount);
        }

        return Array.AsReadOnly(result);
    }
}
