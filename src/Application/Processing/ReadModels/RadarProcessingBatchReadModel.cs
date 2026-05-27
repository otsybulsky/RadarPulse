using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing read model for one provider batch.
/// </summary>
public sealed class RadarProcessingBatchReadModel
{
    /// <summary>
    /// Creates a batch read model with optional processing result evidence.
    /// </summary>
    public RadarProcessingBatchReadModel(
        long providerSequence,
        bool wasAccepted,
        int streamEventCount = 0,
        int payloadBytes = 0,
        long payloadValueCount = 0,
        long rawValueChecksum = 0,
        RadarProcessingQueuedBatchProcessingStatus? processingStatus = null,
        string message = "",
        RadarProcessingTopologyVersion? topologyVersion = null,
        RadarProcessingMetrics? metrics = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(providerSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(streamEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);
        ArgumentNullException.ThrowIfNull(message);
        if (processingStatus.HasValue)
        {
            EnsureKnownProcessingStatus(processingStatus.Value);
        }

        ProviderSequence = providerSequence;
        WasAccepted = wasAccepted;
        StreamEventCount = streamEventCount;
        PayloadBytes = payloadBytes;
        PayloadValueCount = payloadValueCount;
        RawValueChecksum = rawValueChecksum;
        ProcessingStatus = processingStatus;
        Message = message;
        TopologyVersion = topologyVersion;
        Metrics = metrics ?? RadarProcessingMetrics.Empty;
    }

    /// <summary>
    /// Provider sequence used for deterministic ordering.
    /// </summary>
    public long ProviderSequence { get; }

    /// <summary>
    /// Indicates whether the provider accepted the batch.
    /// </summary>
    public bool WasAccepted { get; }

    /// <summary>
    /// Stream event count in the accepted batch.
    /// </summary>
    public int StreamEventCount { get; }

    /// <summary>
    /// Payload bytes in the accepted batch.
    /// </summary>
    public int PayloadBytes { get; }

    /// <summary>
    /// Payload value count in the accepted batch.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Raw payload checksum in the accepted batch.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Processing outcome when available.
    /// </summary>
    public RadarProcessingQueuedBatchProcessingStatus? ProcessingStatus { get; }

    /// <summary>
    /// Indicates whether processing status evidence exists.
    /// </summary>
    public bool HasProcessingStatus => ProcessingStatus.HasValue;

    /// <summary>
    /// Indicates whether processing succeeded.
    /// </summary>
    public bool IsSuccessful => ProcessingStatus == RadarProcessingQueuedBatchProcessingStatus.Succeeded;

    /// <summary>
    /// Optional batch diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Topology version observed during processing when available.
    /// </summary>
    public RadarProcessingTopologyVersion? TopologyVersion { get; }

    /// <summary>
    /// Processing metrics associated with the batch when available.
    /// </summary>
    public RadarProcessingMetrics Metrics { get; }

    private static void EnsureKnownProcessingStatus(
        RadarProcessingQueuedBatchProcessingStatus status)
    {
        if (status is not RadarProcessingQueuedBatchProcessingStatus.Succeeded and
            not RadarProcessingQueuedBatchProcessingStatus.FailedProcessing and
            not RadarProcessingQueuedBatchProcessingStatus.FailedValidation and
            not RadarProcessingQueuedBatchProcessingStatus.FailedMigration and
            not RadarProcessingQueuedBatchProcessingStatus.Canceled and
            not RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
