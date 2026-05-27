using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingBatchReadModel
{
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

    public long ProviderSequence { get; }

    public bool WasAccepted { get; }

    public int StreamEventCount { get; }

    public int PayloadBytes { get; }

    public long PayloadValueCount { get; }

    public long RawValueChecksum { get; }

    public RadarProcessingQueuedBatchProcessingStatus? ProcessingStatus { get; }

    public bool HasProcessingStatus => ProcessingStatus.HasValue;

    public bool IsSuccessful => ProcessingStatus == RadarProcessingQueuedBatchProcessingStatus.Succeeded;

    public string Message { get; }

    public RadarProcessingTopologyVersion? TopologyVersion { get; }

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
