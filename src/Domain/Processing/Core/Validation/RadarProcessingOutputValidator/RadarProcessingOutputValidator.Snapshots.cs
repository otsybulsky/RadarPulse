using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingOutputValidator
{
    private static RadarProcessingValidationResult ValidateSnapshots(
        IReadOnlyList<RadarSourceProcessingSnapshot> expectedSnapshots,
        IReadOnlyList<RadarSourceProcessingSnapshot> afterSnapshots,
        RadarProcessingMetrics actualMetrics,
        RadarProcessingMetrics expectedMetrics)
    {
        for (var sourceId = 0; sourceId < expectedSnapshots.Count; sourceId++)
        {
            if (afterSnapshots[sourceId] != expectedSnapshots[sourceId])
            {
                return RadarProcessingValidationResult.Invalid(
                    RadarProcessingValidationError.MetricsMismatch,
                    sourceId,
                    -1,
                    "Source processing snapshot does not match the expected batch output.",
                    actualMetrics,
                    expectedMetrics);
            }
        }

        return RadarProcessingValidationResult.Valid(actualMetrics);
    }

    private static RadarProcessingMetrics CreateMetrics(
        IReadOnlyList<RadarSourceProcessingSnapshot> snapshots,
        long processedBatchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(processedBatchCount);

        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;
        var activeSourceCount = 0L;
        var rawValueChecksum = 0L;
        var processingChecksum = 0UL;

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.IsActive)
            {
                continue;
            }

            if (activeSourceCount == 0)
            {
                processingChecksum = RadarStreamChecksum.Initial;
            }

            activeSourceCount = checked(activeSourceCount + 1);
            processedStreamEventCount = checked(processedStreamEventCount + snapshot.ProcessedEventCount);
            processedPayloadValueCount = checked(
                processedPayloadValueCount + snapshot.ProcessedPayloadValueCount);
            rawValueChecksum = checked(rawValueChecksum + snapshot.RawValueChecksum);
            processingChecksum = RadarSourceProcessingChecksum.AppendSource(
                processingChecksum,
                snapshot.SourceId,
                snapshot.ProcessedEventCount,
                snapshot.ProcessedPayloadValueCount,
                snapshot.RawValueChecksum,
                snapshot.LastMessageTimestampUtcTicks,
                snapshot.ProcessingChecksum);
        }

        return new RadarProcessingMetrics(
            processedBatchCount,
            processedStreamEventCount,
            processedPayloadValueCount,
            activeSourceCount,
            rawValueChecksum,
            processingChecksum);
    }

    private static RadarSourceProcessingSnapshot[] CreateExpectedSnapshots(
        IReadOnlyList<RadarSourceProcessingSnapshot> beforeSnapshots)
    {
        var result = new RadarSourceProcessingSnapshot[beforeSnapshots.Count];
        for (var sourceId = 0; sourceId < result.Length; sourceId++)
        {
            result[sourceId] = beforeSnapshots[sourceId];
        }

        return result;
    }
}
