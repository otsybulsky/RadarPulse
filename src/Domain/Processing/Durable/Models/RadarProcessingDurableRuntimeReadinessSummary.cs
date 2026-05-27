namespace RadarPulse.Domain.Processing;

/// <summary>
/// Runtime readiness summary derived from durable queue and retained-resource evidence.
/// </summary>
/// <remarks>
/// Readiness requires no uncommitted/blocking envelopes, no release failures, and
/// no terminal retained pressure. The summary is the domain-level source for
/// product/operator readiness messages.
/// </remarks>
public sealed class RadarProcessingDurableRuntimeReadinessSummary
{
    /// <summary>
    /// Empty runtime readiness summary.
    /// </summary>
    public static RadarProcessingDurableRuntimeReadinessSummary Empty { get; } = new();

    /// <summary>
    /// Creates runtime readiness from durable queue and retained-resource terminal evidence.
    /// </summary>
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

    /// <summary>
    /// Durable queue summary backing this readiness calculation.
    /// </summary>
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

    /// <summary>
    /// Number of retained payload release failures observed by the durable runtime.
    /// </summary>
    public long ReleaseFailureCount { get; }

    /// <summary>
    /// Number of terminal envelopes still retaining payload ownership.
    /// </summary>
    public long TerminalRetainedEnvelopeCount { get; }

    /// <summary>
    /// Payload bytes still retained by terminal envelopes.
    /// </summary>
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

    /// <summary>
    /// First readiness blocker, or an empty string when the runtime is ready.
    /// </summary>
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
