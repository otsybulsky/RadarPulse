namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDurableEnvelopeSnapshot
{
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

    public RadarProcessingDurableBatchId BatchId { get; }

    public RadarProcessingQueuedBatchSequence ProviderSequence { get; }

    public int Attempt { get; }

    public RadarProcessingDurableEnvelopeState State { get; }

    public string WorkerId { get; }

    public string Message { get; }

    public int StreamEventCount { get; }

    public int PayloadBytes { get; }

    public long PayloadValueCount { get; }

    public long RawValueChecksum { get; }

    public long AcceptedTimestamp { get; }

    public long ClaimedTimestamp { get; }

    public long CompletedTimestamp { get; }

    public long CommittedTimestamp { get; }

    public long ReleasedTimestamp { get; }

    public bool IsCommitted => State == RadarProcessingDurableEnvelopeState.Committed;

    public bool IsReleased => State == RadarProcessingDurableEnvelopeState.Released;

    internal static void EnsureKnownState(
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
