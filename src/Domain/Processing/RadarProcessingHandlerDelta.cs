using System.Globalization;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingHandlerDelta
{
    public const int CurrentSchemaVersion = 1;

    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> values;

    public RadarProcessingHandlerDelta(
        string handlerName,
        string handlerContractVersion,
        RadarProcessingQueuedBatchSequence providerSequence,
        RadarProcessingDurableBatchId? durableBatchId,
        int eventCount,
        int sourceCount,
        long payloadValueCount,
        long inputChecksum,
        RadarProcessingHandlerDeltaId deltaId,
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerContractVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "Handler delta schema version is not supported.");
        }

        var expectedDeltaId = CreateId(
            handlerName,
            handlerContractVersion,
            providerSequence,
            durableBatchId,
            eventCount,
            sourceCount,
            payloadValueCount,
            inputChecksum);
        if (deltaId != expectedDeltaId)
        {
            throw new ArgumentException(
                "Handler delta id must match handler and batch identity.",
                nameof(deltaId));
        }

        HandlerName = handlerName;
        HandlerContractVersion = handlerContractVersion;
        ProviderSequence = providerSequence;
        DurableBatchId = durableBatchId;
        EventCount = eventCount;
        SourceCount = sourceCount;
        PayloadValueCount = payloadValueCount;
        InputChecksum = inputChecksum;
        DeltaId = deltaId;
        SchemaVersion = schemaVersion;
        this.values = CopyValues(values, sourceCount);
    }

    public string HandlerName { get; }

    public string HandlerContractVersion { get; }

    public RadarProcessingQueuedBatchSequence ProviderSequence { get; }

    public RadarProcessingDurableBatchId? DurableBatchId { get; }

    public int EventCount { get; }

    public int SourceCount { get; }

    public long PayloadValueCount { get; }

    public long InputChecksum { get; }

    public RadarProcessingHandlerDeltaId DeltaId { get; }

    public int SchemaVersion { get; }

    public IReadOnlyList<RadarProcessingHandlerDeltaValue> Values => values;

    public static RadarProcessingHandlerDelta Create(
        string handlerName,
        string handlerContractVersion,
        RadarProcessingQueuedBatchSequence providerSequence,
        RadarProcessingDurableBatchId? durableBatchId,
        int eventCount,
        int sourceCount,
        long payloadValueCount,
        long inputChecksum,
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values = null) =>
        new(
            handlerName,
            handlerContractVersion,
            providerSequence,
            durableBatchId,
            eventCount,
            sourceCount,
            payloadValueCount,
            inputChecksum,
            CreateId(
                handlerName,
                handlerContractVersion,
                providerSequence,
                durableBatchId,
                eventCount,
                sourceCount,
                payloadValueCount,
                inputChecksum),
            values);

    public static RadarProcessingHandlerDeltaId CreateId(
        string handlerName,
        string handlerContractVersion,
        RadarProcessingQueuedBatchSequence providerSequence,
        RadarProcessingDurableBatchId? durableBatchId,
        int eventCount,
        int sourceCount,
        long payloadValueCount,
        long inputChecksum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerContractVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);

        var durableValue = durableBatchId?.Value ?? "none";
        var value = string.Join(
            "|",
            handlerName,
            handlerContractVersion,
            providerSequence.Value.ToString(CultureInfo.InvariantCulture),
            durableValue,
            eventCount.ToString(CultureInfo.InvariantCulture),
            sourceCount.ToString(CultureInfo.InvariantCulture),
            payloadValueCount.ToString(CultureInfo.InvariantCulture),
            inputChecksum.ToString(CultureInfo.InvariantCulture));
        return new RadarProcessingHandlerDeltaId(value);
    }

    private static IReadOnlyList<RadarProcessingHandlerDeltaValue> CopyValues(
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values,
        int sourceCount)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<RadarProcessingHandlerDeltaValue>();
        }

        var result = new RadarProcessingHandlerDeltaValue[values.Count];
        var seen = new HashSet<(int SourceId, string FieldName)>();
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if ((uint)value.SourceId >= (uint)sourceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(values));
            }

            RadarProcessingHandlerDeltaValue.EnsureKnownType(value.Type);
            if (!seen.Add((value.SourceId, value.FieldName)))
            {
                throw new ArgumentException(
                    "Handler delta values must not contain duplicate source and field pairs.",
                    nameof(values));
            }

            result[i] = value;
        }

        return Array.AsReadOnly(result);
    }
}
