using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static class RadarProcessingOutputValidator
{
    public static RadarProcessingValidationResult Validate(
        RadarEventBatch batch,
        RadarProcessingResult result,
        IReadOnlyList<RadarSourceProcessingSnapshot> beforeSnapshots,
        IReadOnlyList<RadarSourceProcessingSnapshot> afterSnapshots,
        RadarProcessingMetrics? previousMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(beforeSnapshots);
        ArgumentNullException.ThrowIfNull(afterSnapshots);

        EnsureSnapshotShape(beforeSnapshots, nameof(beforeSnapshots));
        EnsureSnapshotShape(afterSnapshots, nameof(afterSnapshots));

        if (beforeSnapshots.Count != afterSnapshots.Count)
        {
            throw new ArgumentException("Before and after snapshot counts must match.", nameof(afterSnapshots));
        }

        if (!result.IsValid)
        {
            return result.Validation;
        }

        if (result.Validation.Metrics != result.Metrics)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Processing validation metrics do not match result metrics.",
                result.Validation.Metrics,
                result.Metrics);
        }

        if (batch.StreamSchemaVersion != StreamSchemaVersion.Current)
        {
            return Invalid(
                RadarProcessingValidationError.UnsupportedStreamSchemaVersion,
                -1,
                -1,
                "Unsupported stream schema version.");
        }

        var previousBatchCount = previousMetrics?.ProcessedBatchCount ?? result.Metrics.ProcessedBatchCount;
        var beforeMetrics = CreateMetrics(beforeSnapshots, previousBatchCount);
        if (previousMetrics.HasValue && beforeMetrics != previousMetrics.Value)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Previous processing metrics do not match the supplied before snapshots.",
                previousMetrics.Value,
                beforeMetrics);
        }

        var expectedSnapshots = CreateExpectedSnapshots(beforeSnapshots);
        var expectedResult = ApplyBatch(batch, expectedSnapshots);
        if (!expectedResult.IsValid)
        {
            return expectedResult;
        }

        var expectedBatchCount = previousMetrics.HasValue
            ? checked(previousMetrics.Value.ProcessedBatchCount + 1)
            : result.Metrics.ProcessedBatchCount;
        var expectedMetrics = CreateMetrics(expectedSnapshots, expectedBatchCount);
        var actualSnapshotMetrics = CreateMetrics(afterSnapshots, result.Metrics.ProcessedBatchCount);

        var snapshotResult = ValidateSnapshots(
            expectedSnapshots,
            afterSnapshots,
            actualSnapshotMetrics,
            expectedMetrics);
        if (!snapshotResult.IsValid)
        {
            return snapshotResult;
        }

        if (actualSnapshotMetrics != result.Metrics)
        {
            return RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                -1,
                -1,
                "Processing result metrics do not match the supplied after snapshots.",
                result.Metrics,
                actualSnapshotMetrics);
        }

        if (result.Metrics != expectedMetrics)
        {
            var error = result.Metrics.ProcessedPayloadValueCount == expectedMetrics.ProcessedPayloadValueCount
                ? RadarProcessingValidationError.MetricsMismatch
                : RadarProcessingValidationError.PayloadValueCountMismatch;
            return RadarProcessingValidationResult.Invalid(
                error,
                -1,
                -1,
                "Processing result metrics do not match the expected batch output.",
                result.Metrics,
                expectedMetrics);
        }

        var telemetryResult = ValidateTelemetry(batch, result, expectedMetrics);
        if (!telemetryResult.IsValid)
        {
            return telemetryResult;
        }

        return RadarProcessingValidationResult.Valid(result.Metrics);
    }

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

    private static RadarProcessingValidationResult ApplyBatch(
        RadarEventBatch batch,
        RadarSourceProcessingSnapshot[] expectedSnapshots)
    {
        var events = batch.Events.Span;
        var payload = batch.Payload.Span;

        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            var streamEvent = events[eventIndex];
            if ((uint)streamEvent.SourceId >= (uint)expectedSnapshots.Length)
            {
                return RadarProcessingValidationResult.Invalid(
                    RadarProcessingValidationError.SourceIdOutsideUniverse,
                    streamEvent.SourceId,
                    eventIndex,
                    "Event SourceId is outside the supplied processing snapshots.");
            }

            var snapshot = expectedSnapshots[streamEvent.SourceId];
            if (snapshot.IsActive &&
                streamEvent.MessageTimestampUtcTicks < snapshot.LastMessageTimestampUtcTicks)
            {
                return RadarProcessingValidationResult.Invalid(
                    RadarProcessingValidationError.SourceOrderViolation,
                    streamEvent.SourceId,
                    eventIndex,
                    "Source-local events must be applied by non-decreasing message timestamp.");
            }

            var payloadMetrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, payload);
            expectedSnapshots[streamEvent.SourceId] = ApplyExpectedEvent(
                snapshot,
                streamEvent,
                payloadMetrics);
        }

        return RadarProcessingValidationResult.Valid(default);
    }

    private static RadarSourceProcessingSnapshot ApplyExpectedEvent(
        RadarSourceProcessingSnapshot snapshot,
        in RadarStreamEvent streamEvent,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        var checksum = RadarSourceProcessingChecksum.AppendEvent(
            snapshot.IsActive ? snapshot.ProcessingChecksum : RadarStreamChecksum.Initial,
            streamEvent,
            payloadMetrics.PayloadValueCount,
            payloadMetrics.RawValueChecksum);

        return new RadarSourceProcessingSnapshot(
            snapshot.SourceId,
            true,
            checked(snapshot.ProcessedEventCount + 1),
            checked(snapshot.ProcessedPayloadValueCount + payloadMetrics.PayloadValueCount),
            checked(snapshot.RawValueChecksum + payloadMetrics.RawValueChecksum),
            streamEvent.MessageTimestampUtcTicks,
            checksum);
    }

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

    private static void EnsureSnapshotShape(
        IReadOnlyList<RadarSourceProcessingSnapshot> snapshots,
        string paramName)
    {
        for (var sourceId = 0; sourceId < snapshots.Count; sourceId++)
        {
            if (snapshots[sourceId].SourceId != sourceId)
            {
                throw new ArgumentException(
                    "Processing snapshots must be ordered by SourceId.",
                    paramName);
            }
        }
    }

    private static RadarProcessingValidationResult Invalid(
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message) =>
        RadarProcessingValidationResult.Invalid(error, sourceId, eventIndex, message);
}
