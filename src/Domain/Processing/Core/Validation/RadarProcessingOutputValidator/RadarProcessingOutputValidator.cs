using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Recomputes expected processing output and compares it with result metrics and source snapshots.
/// </summary>
public static partial class RadarProcessingOutputValidator
{
    /// <summary>
    /// Validates a processing result against before/after snapshots and optional previous metrics.
    /// </summary>
    /// <returns>A valid result when output is deterministic, otherwise the first detected mismatch.</returns>
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
}
