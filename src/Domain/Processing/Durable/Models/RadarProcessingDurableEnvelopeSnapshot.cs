namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable snapshot of durable envelope state and metrics.
/// </summary>
/// <remarks>
/// Snapshots are the durable queue's observable contract. They carry lifecycle
/// state, provider sequence, attempt number, worker/message diagnostics, payload
/// metrics, and monotonic timestamps without exposing store internals.
/// </remarks>
public sealed class RadarProcessingDurableEnvelopeSnapshot
{
    /// <summary>
    /// Creates a durable envelope snapshot with validated state and non-negative metrics.
    /// </summary>
    public RadarProcessingDurableEnvelopeSnapshot(
        RadarProcessingDurableBatchId batchId,
        RadarProcessingQueuedBatchSequence providerSequence,
        int attempt,
        RadarProcessingDurableEnvelopeState state,
        string workerId = "",
        string message = "",
        int streamEventCount = 0,
        int payloadBytes = 0,
        long payloadValueCount = 0,
        long rawValueChecksum = 0,
        long acceptedTimestamp = 0,
        long claimedTimestamp = 0,
        long completedTimestamp = 0,
        long committedTimestamp = 0,
        long releasedTimestamp = 0)
    {
        if (string.IsNullOrWhiteSpace(batchId.Value))
        {
            throw new ArgumentException("Durable batch id must not be empty.", nameof(batchId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        EnsureKnownState(state);
        ArgumentNullException.ThrowIfNull(workerId);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegative(streamEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(claimedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(completedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(committedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(releasedTimestamp);

        BatchId = batchId;
        ProviderSequence = providerSequence;
        Attempt = attempt;
        State = state;
        WorkerId = workerId;
        Message = message;
        StreamEventCount = streamEventCount;
        PayloadBytes = payloadBytes;
        PayloadValueCount = payloadValueCount;
        RawValueChecksum = rawValueChecksum;
        AcceptedTimestamp = acceptedTimestamp;
        ClaimedTimestamp = claimedTimestamp;
        CompletedTimestamp = completedTimestamp;
        CommittedTimestamp = committedTimestamp;
        ReleasedTimestamp = releasedTimestamp;
    }

    /// <summary>
    /// Stable durable envelope id.
    /// </summary>
    public RadarProcessingDurableBatchId BatchId { get; }

    /// <summary>
    /// Provider ordering key for the batch represented by this envelope.
    /// </summary>
    public RadarProcessingQueuedBatchSequence ProviderSequence { get; }

    /// <summary>
    /// Processing attempt count for this envelope.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Current durable lifecycle state.
    /// </summary>
    public RadarProcessingDurableEnvelopeState State { get; }

    /// <summary>
    /// Worker id that last claimed or updated the envelope.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Optional diagnostic message associated with the current state.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Number of stream events in the envelope payload.
    /// </summary>
    public int StreamEventCount { get; }

    /// <summary>
    /// Envelope payload byte length.
    /// </summary>
    public int PayloadBytes { get; }

    /// <summary>
    /// Envelope payload value count.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Deterministic checksum of raw payload values.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Timestamp captured when the envelope was accepted.
    /// </summary>
    public long AcceptedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when the envelope was claimed.
    /// </summary>
    public long ClaimedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when processing completed.
    /// </summary>
    public long CompletedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when processing output was committed.
    /// </summary>
    public long CommittedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when retained resources were released.
    /// </summary>
    public long ReleasedTimestamp { get; }

    /// <summary>
    /// Indicates that the envelope output has been committed.
    /// </summary>
    public bool IsCommitted => State == RadarProcessingDurableEnvelopeState.Committed;

    /// <summary>
    /// Indicates that terminal retained resources have been released.
    /// </summary>
    public bool IsReleased => State == RadarProcessingDurableEnvelopeState.Released;

    /// <summary>
    /// Throws when the supplied durable envelope state is unknown.
    /// </summary>
    public static void EnsureKnownState(
        RadarProcessingDurableEnvelopeState state)
    {
        if (state is not RadarProcessingDurableEnvelopeState.Pending and
            not RadarProcessingDurableEnvelopeState.Claimed and
            not RadarProcessingDurableEnvelopeState.Completed and
            not RadarProcessingDurableEnvelopeState.Committed and
            not RadarProcessingDurableEnvelopeState.Failed and
            not RadarProcessingDurableEnvelopeState.Poison and
            not RadarProcessingDurableEnvelopeState.Abandoned and
            not RadarProcessingDurableEnvelopeState.Canceled and
            not RadarProcessingDurableEnvelopeState.Released)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }
}
