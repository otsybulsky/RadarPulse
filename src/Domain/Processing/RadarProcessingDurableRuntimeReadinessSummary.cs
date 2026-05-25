namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDurableRuntimeReadinessSummary
{
    public static RadarProcessingDurableRuntimeReadinessSummary Empty { get; } = new();

    public RadarProcessingDurableRuntimeReadinessSummary(
        RadarProcessingDurableQueueSummary? queueSummary = null,
        long releaseFailureCount = 0,
        long terminalRetainedEnvelopeCount = 0,
        long terminalRetainedPayloadBytes = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(releaseFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(terminalRetainedEnvelopeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(terminalRetainedPayloadBytes);

        QueueSummary = queueSummary ?? RadarProcessingDurableQueueSummary.Empty;
        ReleaseFailureCount = releaseFailureCount;
        TerminalRetainedEnvelopeCount = terminalRetainedEnvelopeCount;
        TerminalRetainedPayloadBytes = terminalRetainedPayloadBytes;
    }

    public RadarProcessingDurableQueueSummary QueueSummary { get; }

    public long AcceptedEnvelopeCount => QueueSummary.AcceptedEnvelopeCount;

    public long PendingEnvelopeCount => QueueSummary.PendingEnvelopeCount;

    public long ClaimedEnvelopeCount => QueueSummary.ClaimedEnvelopeCount;

    public long CompletedEnvelopeCount => QueueSummary.CompletedEnvelopeCount;

    public long CommittedEnvelopeCount => QueueSummary.CommittedEnvelopeCount;

    public long FailedEnvelopeCount => QueueSummary.FailedEnvelopeCount;

    public long PoisonEnvelopeCount => QueueSummary.PoisonEnvelopeCount;

    public long AbandonedAttemptCount => QueueSummary.AbandonedAttemptCount;

    public long CanceledEnvelopeCount => QueueSummary.CanceledEnvelopeCount;

    public long ReleasedEnvelopeCount => QueueSummary.ReleasedEnvelopeCount;

    public long RetryAttemptCount => QueueSummary.RetryAttemptCount;

    public long ReleaseFailureCount { get; }

    public long TerminalRetainedEnvelopeCount { get; }

    public long TerminalRetainedPayloadBytes { get; }

    public RadarProcessingQueuedBatchSequence? OldestUncommittedSequence =>
        QueueSummary.OldestUncommittedSequence;

    public RadarProcessingDurableBatchId? FirstBlockingBatchId =>
        QueueSummary.FirstBlockingBatchId;

    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence =>
        QueueSummary.FirstBlockingSequence;

    public RadarProcessingDurableEnvelopeState? FirstBlockingState =>
        QueueSummary.FirstBlockingState;

    public string FirstBlockingReason => QueueSummary.FirstBlockingReason;

    public bool HasUncommittedEnvelope => QueueSummary.HasUncommittedEnvelope;

    public bool HasBlockingEnvelope => QueueSummary.HasBlockingEnvelope;

    public bool HasReleaseFailures => ReleaseFailureCount > 0;

    public bool HasTerminalRetainedPressure =>
        TerminalRetainedEnvelopeCount > 0 ||
        TerminalRetainedPayloadBytes > 0;

    public bool IsReady =>
        !HasUncommittedEnvelope &&
        !HasBlockingEnvelope &&
        !HasReleaseFailures &&
        !HasTerminalRetainedPressure;

    public string BlockingReason
    {
        get
        {
            if (HasBlockingEnvelope)
            {
                return FirstBlockingReason;
            }

            if (HasUncommittedEnvelope)
            {
                return OldestUncommittedSequence.HasValue
                    ? $"oldest uncommitted provider sequence {OldestUncommittedSequence.Value.Value}"
                    : "uncommitted durable envelope";
            }

            if (HasReleaseFailures)
            {
                return $"release failures {ReleaseFailureCount}";
            }

            if (HasTerminalRetainedPressure)
            {
                return $"terminal retained pressure envelopes {TerminalRetainedEnvelopeCount}, bytes {TerminalRetainedPayloadBytes}";
            }

            return string.Empty;
        }
    }
}
