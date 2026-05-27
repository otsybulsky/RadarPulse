namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Validation errors for radar event batch structure and contracts.
/// </summary>
public enum RadarEventBatchValidationError
{
    /// <summary>
    /// No validation error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Batch uses a stream schema version unsupported by the validator.
    /// </summary>
    UnsupportedStreamSchemaVersion,

    /// <summary>
    /// Batch source-universe version does not match the supplied universe.
    /// </summary>
    SourceUniverseVersionMismatch,

    /// <summary>
    /// Supplied dictionary snapshot version does not match the batch.
    /// </summary>
    DictionarySnapshotVersionMismatch,

    /// <summary>
    /// Events are not ordered by non-decreasing message timestamp.
    /// </summary>
    ChronologyOrderViolation,

    /// <summary>
    /// Event payload length does not match gate count and word size.
    /// </summary>
    PayloadLengthMismatch,

    /// <summary>
    /// Event payload reference exceeds batch payload storage.
    /// </summary>
    PayloadReferenceOutsidePayload,

    /// <summary>
    /// Event payload references are not contiguous and ordered.
    /// </summary>
    PayloadReferenceNotContiguous,

    /// <summary>
    /// Batch payload contains unreferenced trailing bytes.
    /// </summary>
    PayloadTailNotReferenced,

    /// <summary>
    /// Event source id is outside the supplied source universe.
    /// </summary>
    SourceIdOutsideUniverse,

    /// <summary>
    /// Event source dimensions are outside the supplied source universe.
    /// </summary>
    SourceKeyOutsideUniverse,

    /// <summary>
    /// Event source id does not match its source dimensions.
    /// </summary>
    SourceKeyMismatch,

    /// <summary>
    /// Event radar ordinal is not visible in the supplied dictionary snapshot.
    /// </summary>
    RadarOrdinalOutsideDictionary,

    /// <summary>
    /// Event moment id is not visible in the supplied dictionary snapshot.
    /// </summary>
    MomentIdOutsideDictionary,

    /// <summary>
    /// Computed batch metrics do not match expected metrics.
    /// </summary>
    MetricsMismatch
}
