namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDurableQueueSummary
{
    public static RadarProcessingDurableQueueSummary Empty { get; } = new();

    public RadarProcessingDurableQueueSummary(
        long acceptedEnvelopeCount = 0,
        long pendingEnvelopeCount = 0,
        long claimedEnvelopeCount = 0,
        long completedEnvelopeCount = 0,
        long committedEnvelopeCount = 0,
        long failedEnvelopeCount = 0,
        long poisonEnvelopeCount = 0,
        long abandonedAttemptCount = 0,
        long canceledEnvelopeCount = 0,
        long releasedEnvelopeCount = 0,
        long retryAttemptCount = 0,
        RadarProcessingQueuedBatchSequence? oldestUncommittedSequence = null,
        RadarProcessingDurableBatchId? firstBlockingBatchId = null,
        RadarProcessingQueuedBatchSequence? firstBlockingSequence = null,
        RadarProcessingDurableEnvelopeState? firstBlockingState = null,
        string firstBlockingReason = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(claimedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(committedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(poisonEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(abandonedAttemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(releasedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retryAttemptCount);
        ArgumentNullException.ThrowIfNull(firstBlockingReason);
        if (firstBlockingState.HasValue)
        {
            RadarProcessingDurableEnvelopeSnapshot.EnsureKnownState(firstBlockingState.Value);
        }

        AcceptedEnvelopeCount = acceptedEnvelopeCount;
        PendingEnvelopeCount = pendingEnvelopeCount;
        ClaimedEnvelopeCount = claimedEnvelopeCount;
        CompletedEnvelopeCount = completedEnvelopeCount;
        CommittedEnvelopeCount = committedEnvelopeCount;
        FailedEnvelopeCount = failedEnvelopeCount;
        PoisonEnvelopeCount = poisonEnvelopeCount;
        AbandonedAttemptCount = abandonedAttemptCount;
        CanceledEnvelopeCount = canceledEnvelopeCount;
        ReleasedEnvelopeCount = releasedEnvelopeCount;
        RetryAttemptCount = retryAttemptCount;
        OldestUncommittedSequence = oldestUncommittedSequence;
        FirstBlockingBatchId = firstBlockingBatchId;
        FirstBlockingSequence = firstBlockingSequence;
        FirstBlockingState = firstBlockingState;
        FirstBlockingReason = firstBlockingReason;
    }

    public long AcceptedEnvelopeCount { get; }

    public long PendingEnvelopeCount { get; }

    public long ClaimedEnvelopeCount { get; }

    public long CompletedEnvelopeCount { get; }

    public long CommittedEnvelopeCount { get; }

    public long FailedEnvelopeCount { get; }

    public long PoisonEnvelopeCount { get; }

    public long AbandonedAttemptCount { get; }

    public long CanceledEnvelopeCount { get; }

    public long ReleasedEnvelopeCount { get; }

    public long RetryAttemptCount { get; }

    public RadarProcessingQueuedBatchSequence? OldestUncommittedSequence { get; }

    public RadarProcessingDurableBatchId? FirstBlockingBatchId { get; }

    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence { get; }

    public RadarProcessingDurableEnvelopeState? FirstBlockingState { get; }

    public string FirstBlockingReason { get; }

    public bool HasUncommittedEnvelope => OldestUncommittedSequence.HasValue;

    public bool HasBlockingEnvelope => FirstBlockingBatchId.HasValue;
}
