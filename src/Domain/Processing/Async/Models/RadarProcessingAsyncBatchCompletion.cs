namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregates work item completions for one async batch scope.
/// </summary>
public sealed record RadarProcessingAsyncBatchCompletion
{
    private readonly IReadOnlyList<RadarProcessingAsyncWorkCompletion> completions;

    /// <summary>
    /// Creates a completion aggregate and validates completion scope, uniqueness, and counters.
    /// </summary>
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

    /// <summary>
    /// Gets the batch sequence represented by the aggregate.
    /// </summary>
    public long BatchSequence { get; }

    /// <summary>
    /// Gets the topology version shared by all completions.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Gets the number of work items expected for the batch.
    /// </summary>
    public int ExpectedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of recorded work item completions.
    /// </summary>
    public int RecordedWorkItemCount => completions.Count;

    /// <summary>
    /// Gets the number of successful work item completions.
    /// </summary>
    public int SucceededWorkItemCount { get; }

    /// <summary>
    /// Gets the number of failed work item completions.
    /// </summary>
    public int FailedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of canceled work item completions.
    /// </summary>
    public int CanceledWorkItemCount { get; }

    /// <summary>
    /// Gets total queue wait time across recorded work items.
    /// </summary>
    public TimeSpan TotalQueueWaitTime { get; }

    /// <summary>
    /// Gets total execution time across recorded work items.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets total processed stream events across recorded work items.
    /// </summary>
    public long ProcessedStreamEventCount { get; }

    /// <summary>
    /// Gets total processed payload values across recorded work items.
    /// </summary>
    public long ProcessedPayloadValueCount { get; }

    /// <summary>
    /// Gets whether the owning scope was closed when this snapshot was created.
    /// </summary>
    public bool IsClosed { get; }

    /// <summary>
    /// Gets whether every expected work item has a recorded completion.
    /// </summary>
    public bool IsComplete => RecordedWorkItemCount == ExpectedWorkItemCount;

    /// <summary>
    /// Gets whether the aggregate is complete and has no failed or canceled work.
    /// </summary>
    public bool IsSuccessful =>
        IsComplete &&
        FailedWorkItemCount == 0 &&
        CanceledWorkItemCount == 0;

    /// <summary>
    /// Gets immutable recorded completions.
    /// </summary>
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
