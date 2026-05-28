using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticBenchmark
{
    private readonly record struct ValidationRunResult(
        ulong Checksum,
        RadarProcessingAsyncValidationResult? AsyncValidation);

    private readonly record struct ProcessingIterationResult(
        IterationTotals Totals,
        RadarProcessingResult LastResult);

    private readonly record struct IterationTotals(
        long BatchCount,
        long EventCount,
        long PayloadValueCount,
        long RawValueChecksum,
        long ActiveSourceCount)
    {
        public static IterationTotals Create(
            RadarProcessingMetrics before,
            RadarProcessingMetrics after) =>
            new(
                checked(after.ProcessedBatchCount - before.ProcessedBatchCount),
                checked(after.ProcessedStreamEventCount - before.ProcessedStreamEventCount),
                checked(after.ProcessedPayloadValueCount - before.ProcessedPayloadValueCount),
                checked(after.RawValueChecksum - before.RawValueChecksum),
                after.ActiveSourceCount);

        public bool HasSameTotals(IterationTotals other) =>
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            ActiveSourceCount == other.ActiveSourceCount;
    }

}
