namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Validates radar event batches against stream schema, source-universe, dictionary, and metric contracts.
/// </summary>
/// <remarks>
/// Validation checks that compact source identifiers match the supplied universe, optional dictionary ordinals are
/// visible in the supplied snapshot, payload references are contiguous and bounded, event timestamps are ordered,
/// and deterministic metrics match any expected value.
/// </remarks>
public static class RadarEventBatchValidator
{
    /// <summary>
    /// Validates a batch against the supplied source universe and optional dictionary and metric snapshots.
    /// </summary>
    /// <returns>A valid result with computed metrics, or the first contract violation found.</returns>
    public static RadarEventBatchValidationResult Validate(
        RadarEventBatch batch,
        RadarSourceUniverse sourceUniverse,
        RadarStreamDictionarySnapshot? dictionarySnapshot = null,
        RadarEventBatchMetrics? expectedMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        if (batch.StreamSchemaVersion != StreamSchemaVersion.Current)
        {
            return Invalid(
                RadarEventBatchValidationError.UnsupportedStreamSchemaVersion,
                -1,
                $"Unsupported stream schema version {batch.StreamSchemaVersion}.");
        }

        if (batch.SourceUniverseVersion != sourceUniverse.Version)
        {
            return Invalid(
                RadarEventBatchValidationError.SourceUniverseVersionMismatch,
                -1,
                "Batch source-universe version does not match the supplied source universe.");
        }

        if (dictionarySnapshot is not null && dictionarySnapshot.Version != batch.DictionaryVersion)
        {
            return Invalid(
                RadarEventBatchValidationError.DictionarySnapshotVersionMismatch,
                -1,
                "Batch dictionary version does not match the supplied dictionary snapshot.");
        }

        var structuralResult = ValidateStructure(batch, sourceUniverse, dictionarySnapshot);
        if (!structuralResult.IsValid)
        {
            return structuralResult;
        }

        var metrics = RadarEventBatchMetrics.Compute(batch);
        if (expectedMetrics.HasValue && metrics != expectedMetrics.Value)
        {
            return RadarEventBatchValidationResult.Invalid(
                RadarEventBatchValidationError.MetricsMismatch,
                -1,
                "Batch metrics do not match the expected checksum/count contract.",
                metrics,
                expectedMetrics);
        }

        return RadarEventBatchValidationResult.Valid(metrics);
    }

    private static RadarEventBatchValidationResult ValidateStructure(
        RadarEventBatch batch,
        RadarSourceUniverse sourceUniverse,
        RadarStreamDictionarySnapshot? dictionarySnapshot)
    {
        var events = batch.Events.Span;
        var expectedPayloadOffset = 0;
        var previousMessageTimestampUtcTicks = long.MinValue;

        for (var i = 0; i < events.Length; i++)
        {
            var streamEvent = events[i];

            if (streamEvent.MessageTimestampUtcTicks < previousMessageTimestampUtcTicks)
            {
                return Invalid(
                    RadarEventBatchValidationError.ChronologyOrderViolation,
                    i,
                    "Batch events must be ordered by non-decreasing message timestamp.");
            }

            previousMessageTimestampUtcTicks = streamEvent.MessageTimestampUtcTicks;

            if (streamEvent.PayloadLength != streamEvent.ExpectedPayloadLength)
            {
                return Invalid(
                    RadarEventBatchValidationError.PayloadLengthMismatch,
                    i,
                    "Event payload length does not match gate count and word size.");
            }

            if (streamEvent.PayloadOffset > batch.PayloadLength - streamEvent.PayloadLength)
            {
                return Invalid(
                    RadarEventBatchValidationError.PayloadReferenceOutsidePayload,
                    i,
                    "Event payload reference exceeds batch payload storage.");
            }

            if (streamEvent.PayloadOffset != expectedPayloadOffset)
            {
                return Invalid(
                    RadarEventBatchValidationError.PayloadReferenceNotContiguous,
                    i,
                    "Event payload references must be contiguous and ordered inside batch payload storage.");
            }

            expectedPayloadOffset = checked(expectedPayloadOffset + streamEvent.PayloadLength);

            var sourceValidation = ValidateSource(streamEvent, sourceUniverse, i);
            if (!sourceValidation.IsValid)
            {
                return sourceValidation;
            }

            if (dictionarySnapshot is not null)
            {
                var dictionaryValidation = ValidateDictionaryVisibility(streamEvent, dictionarySnapshot, i);
                if (!dictionaryValidation.IsValid)
                {
                    return dictionaryValidation;
                }
            }
        }

        if (expectedPayloadOffset != batch.PayloadLength)
        {
            return Invalid(
                RadarEventBatchValidationError.PayloadTailNotReferenced,
                -1,
                "Batch payload contains bytes that are not referenced by any event.");
        }

        return RadarEventBatchValidationResult.Valid(default);
    }

    private static RadarEventBatchValidationResult ValidateSource(
        RadarStreamEvent streamEvent,
        RadarSourceUniverse sourceUniverse,
        int eventIndex)
    {
        if ((uint)streamEvent.SourceId >= (uint)sourceUniverse.SourceCount)
        {
            return Invalid(
                RadarEventBatchValidationError.SourceIdOutsideUniverse,
                eventIndex,
                "Event SourceId is outside the supplied source universe.");
        }

        var sourceKey = new RadarSourceKey(
            streamEvent.RadarOrdinal,
            streamEvent.ElevationSlot,
            streamEvent.AzimuthBucket,
            streamEvent.RangeBand);
        if (!sourceUniverse.Contains(sourceKey))
        {
            return Invalid(
                RadarEventBatchValidationError.SourceKeyOutsideUniverse,
                eventIndex,
                "Event source dimensions are outside the supplied source universe.");
        }

        var expectedSourceId = sourceUniverse.GetSourceId(sourceKey);
        if (streamEvent.SourceId != expectedSourceId)
        {
            return Invalid(
                RadarEventBatchValidationError.SourceKeyMismatch,
                eventIndex,
                "Event SourceId does not match its source dimensions.");
        }

        return RadarEventBatchValidationResult.Valid(default);
    }

    private static RadarEventBatchValidationResult ValidateDictionaryVisibility(
        RadarStreamEvent streamEvent,
        RadarStreamDictionarySnapshot dictionarySnapshot,
        int eventIndex)
    {
        if (streamEvent.RadarOrdinal >= dictionarySnapshot.RadarCatalog.Count)
        {
            return Invalid(
                RadarEventBatchValidationError.RadarOrdinalOutsideDictionary,
                eventIndex,
                "Event RadarOrdinal is not visible in the supplied dictionary snapshot.");
        }

        if (streamEvent.MomentId >= dictionarySnapshot.MomentCatalog.Count)
        {
            return Invalid(
                RadarEventBatchValidationError.MomentIdOutsideDictionary,
                eventIndex,
                "Event MomentId is not visible in the supplied dictionary snapshot.");
        }

        return RadarEventBatchValidationResult.Valid(default);
    }

    private static RadarEventBatchValidationResult Invalid(
        RadarEventBatchValidationError error,
        int eventIndex,
        string message) =>
        RadarEventBatchValidationResult.Invalid(error, eventIndex, message);
}
