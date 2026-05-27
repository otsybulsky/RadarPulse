namespace RadarPulse.Domain.Processing;

/// <summary>
/// Serializable representation of a handler delta.
/// </summary>
/// <remarks>
/// This type is intentionally validation-light so storage adapters can load raw
/// persisted data before deserialization validates schema, identity, and value
/// constraints.
/// </remarks>
public sealed class RadarProcessingSerializedHandlerDelta
{
    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> values;

    /// <summary>
    /// Creates a serialized handler delta payload.
    /// </summary>
    public RadarProcessingSerializedHandlerDelta(
        int schemaVersion,
        string handlerName,
        string handlerContractVersion,
        RadarProcessingQueuedBatchSequence providerSequence,
        RadarProcessingDurableBatchId? durableBatchId,
        int eventCount,
        int sourceCount,
        long payloadValueCount,
        long inputChecksum,
        RadarProcessingHandlerDeltaId deltaId,
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values = null)
    {
        ArgumentNullException.ThrowIfNull(handlerName);
        ArgumentNullException.ThrowIfNull(handlerContractVersion);

        SchemaVersion = schemaVersion;
        HandlerName = handlerName;
        HandlerContractVersion = handlerContractVersion;
        ProviderSequence = providerSequence;
        DurableBatchId = durableBatchId;
        EventCount = eventCount;
        SourceCount = sourceCount;
        PayloadValueCount = payloadValueCount;
        InputChecksum = inputChecksum;
        DeltaId = deltaId;
        this.values = values is null || values.Count == 0
            ? Array.Empty<RadarProcessingHandlerDeltaValue>()
            : Array.AsReadOnly(values.ToArray());
    }

    /// <summary>
    /// Serialized schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Handler name.
    /// </summary>
    public string HandlerName { get; }

    /// <summary>
    /// Handler contract version.
    /// </summary>
    public string HandlerContractVersion { get; }

    /// <summary>
    /// Provider sequence for the delta.
    /// </summary>
    public RadarProcessingQueuedBatchSequence ProviderSequence { get; }

    /// <summary>
    /// Durable batch id when present.
    /// </summary>
    public RadarProcessingDurableBatchId? DurableBatchId { get; }

    /// <summary>
    /// Input event count.
    /// </summary>
    public int EventCount { get; }

    /// <summary>
    /// Source count expected by values.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Input payload value count.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Input checksum.
    /// </summary>
    public long InputChecksum { get; }

    /// <summary>
    /// Serialized delta id.
    /// </summary>
    public RadarProcessingHandlerDeltaId DeltaId { get; }

    /// <summary>
    /// Serialized delta values.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerDeltaValue> Values => values;
}
