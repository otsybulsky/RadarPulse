namespace RadarPulse.Domain.Streaming;

public enum RadarEventBatchValidationError
{
    None = 0,
    UnsupportedStreamSchemaVersion,
    SourceUniverseVersionMismatch,
    DictionarySnapshotVersionMismatch,
    ChronologyOrderViolation,
    PayloadLengthMismatch,
    PayloadReferenceOutsidePayload,
    PayloadReferenceNotContiguous,
    PayloadTailNotReferenced,
    SourceIdOutsideUniverse,
    SourceKeyOutsideUniverse,
    SourceKeyMismatch,
    RadarOrdinalOutsideDictionary,
    MomentIdOutsideDictionary,
    MetricsMismatch
}
