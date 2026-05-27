namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingSerializedHandlerDelta
{
    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> values;

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

    public int SchemaVersion { get; }

    public string HandlerName { get; }

    public string HandlerContractVersion { get; }

    public RadarProcessingQueuedBatchSequence ProviderSequence { get; }

    public RadarProcessingDurableBatchId? DurableBatchId { get; }

    public int EventCount { get; }

    public int SourceCount { get; }

    public long PayloadValueCount { get; }

    public long InputChecksum { get; }

    public RadarProcessingHandlerDeltaId DeltaId { get; }

    public IReadOnlyList<RadarProcessingHandlerDeltaValue> Values => values;
}
