using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    /// <summary>
    /// Creates aggregate processing metrics from current source state.
    /// </summary>
    public RadarProcessingMetrics CreateMetrics(long processedBatchCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(processedBatchCount);

        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;
        var rawValueChecksum = 0L;
        var processingChecksum = ActiveSourceCount == 0
            ? 0UL
            : RadarStreamChecksum.Initial;

        for (var sourceId = 0; sourceId < SourceCount; sourceId++)
        {
            if (!activeSources[sourceId])
            {
                continue;
            }

            processedStreamEventCount = checked(
                processedStreamEventCount + processedEventCounts[sourceId]);
            processedPayloadValueCount = checked(
                processedPayloadValueCount + processedPayloadValueCounts[sourceId]);
            rawValueChecksum = checked(rawValueChecksum + rawValueChecksums[sourceId]);
            processingChecksum = RadarSourceProcessingChecksum.AppendSource(
                processingChecksum,
                sourceId,
                processedEventCounts[sourceId],
                processedPayloadValueCounts[sourceId],
                rawValueChecksums[sourceId],
                lastMessageTimestampUtcTicks[sourceId],
                processingChecksums[sourceId]);
        }

        return new RadarProcessingMetrics(
            processedBatchCount,
            processedStreamEventCount,
            processedPayloadValueCount,
            ActiveSourceCount,
            rawValueChecksum,
            processingChecksum);
    }
}
