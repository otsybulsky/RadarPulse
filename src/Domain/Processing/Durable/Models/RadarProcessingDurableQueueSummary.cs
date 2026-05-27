namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate durable queue counts and first-blocker evidence.
/// </summary>
/// <remarks>
/// The summary is used by readiness and recovery paths to identify uncommitted,
/// blocking, failed, poison, canceled, and released envelope posture without
/// loading full payload contents.
/// </remarks>
public sealed class RadarProcessingDurableQueueSummary
{
    /// <summary>
    /// Empty queue summary.
    /// </summary>
    public static RadarProcessingDurableQueueSummary Empty { get; } = new();

    /// <summary>
    /// Creates a queue summary with validated counts and optional first-blocker evidence.
    /// </summary>
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

    /// <summary>
    /// Total envelopes accepted into durable storage.
    /// </summary>
    public long AcceptedEnvelopeCount { get; }

    /// <summary>
    /// Envelopes waiting to be claimed.
    /// </summary>
    public long PendingEnvelopeCount { get; }

    /// <summary>
    /// Envelopes currently claimed by workers.
    /// </summary>
    public long ClaimedEnvelopeCount { get; }

    /// <summary>
    /// Envelopes completed but not necessarily committed.
    /// </summary>
    public long CompletedEnvelopeCount { get; }

    /// <summary>
    /// Envelopes with committed processing output.
    /// </summary>
    public long CommittedEnvelopeCount { get; }

    /// <summary>
    /// Envelopes that failed processing.
    /// </summary>
    public long FailedEnvelopeCount { get; }

    /// <summary>
    /// Envelopes marked unsafe for automatic retry.
    /// </summary>
    public long PoisonEnvelopeCount { get; }

    /// <summary>
    /// Abandoned claim attempts.
    /// </summary>
    public long AbandonedAttemptCount { get; }

    /// <summary>
    /// Envelopes canceled before normal completion.
    /// </summary>
    public long CanceledEnvelopeCount { get; }

    /// <summary>
    /// Terminal envelopes whose retained resources were released.
    /// </summary>
    public long ReleasedEnvelopeCount { get; }

    /// <summary>
    /// Retry attempts recorded by durable recovery.
    /// </summary>
    public long RetryAttemptCount { get; }

    /// <summary>
    /// Oldest provider sequence that still needs commit or recovery.
    /// </summary>
    public RadarProcessingQueuedBatchSequence? OldestUncommittedSequence { get; }

    /// <summary>
    /// Durable id for the first envelope blocking readiness.
    /// </summary>
    public RadarProcessingDurableBatchId? FirstBlockingBatchId { get; }

    /// <summary>
    /// Provider sequence for the first envelope blocking readiness.
    /// </summary>
    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence { get; }

    /// <summary>
    /// State of the first envelope blocking readiness.
    /// </summary>
    public RadarProcessingDurableEnvelopeState? FirstBlockingState { get; }

    /// <summary>
    /// Human-readable first blocking reason.
    /// </summary>
    public string FirstBlockingReason { get; }

    /// <summary>
    /// Indicates whether any envelope still needs commit or recovery.
    /// </summary>
    public bool HasUncommittedEnvelope => OldestUncommittedSequence.HasValue;

    /// <summary>
    /// Indicates whether a specific first blocking envelope was identified.
    /// </summary>
    public bool HasBlockingEnvelope => FirstBlockingBatchId.HasValue;
}
