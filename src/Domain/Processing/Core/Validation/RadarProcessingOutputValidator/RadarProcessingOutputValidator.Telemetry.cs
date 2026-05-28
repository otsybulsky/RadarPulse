using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingOutputValidator
{
    private static RadarProcessingValidationResult ValidateTelemetry(
        RadarEventBatch batch,
        RadarProcessingResult result,
        RadarProcessingMetrics expectedMetrics)
    {
        if (result.ExecutionMode is not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            return RadarProcessingValidationResult.Valid(result.Metrics);
        }

        if (result.Telemetry is null)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Partitioned or async processing result is missing partitioned telemetry.",
                result.Metrics,
                expectedMetrics);
        }

        var batchMetrics = RadarEventBatchMetrics.Compute(batch);
        if (result.Telemetry.BatchMetrics.EventCount != batchMetrics.EventCount ||
            result.Telemetry.BatchMetrics.PayloadValueCount != batchMetrics.PayloadValueCount ||
            result.Telemetry.BatchMetrics.RawValueChecksum != batchMetrics.RawValueChecksum)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Partitioned telemetry batch metrics do not match the processed batch.",
                result.Metrics,
                expectedMetrics);
        }

        if (SumPartitionMetrics(result.Telemetry) != result.Telemetry.BatchMetrics)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Partition telemetry totals do not match telemetry batch metrics.",
                result.Metrics,
                expectedMetrics);
        }

        if (SumShardMetrics(result.Telemetry) != result.Telemetry.BatchMetrics)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Shard telemetry totals do not match telemetry batch metrics.",
                result.Metrics,
                expectedMetrics);
        }

        return RadarProcessingValidationResult.Valid(result.Metrics);
    }

    private static RadarProcessingRouteMetrics SumPartitionMetrics(RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in telemetry.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in telemetry.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }
}
