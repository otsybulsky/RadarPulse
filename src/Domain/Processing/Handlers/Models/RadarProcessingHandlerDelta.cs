using System.Globalization;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Mergeable handler output produced for one provider batch.
/// </summary>
/// <remarks>
/// A delta is immutable and self-identifying. The id must match the handler name,
/// contract version, provider sequence, optional durable batch id, input counts,
/// and input checksum. This prevents unrelated or conflicting delta payloads from
/// being merged under the same identity.
/// </remarks>
public sealed class RadarProcessingHandlerDelta
{
    /// <summary>
    /// Current handler delta schema version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> values;

    /// <summary>
    /// Creates a handler delta with validated identity and copied values.
    /// </summary>
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
        : this(
            handlerName,
            handlerContractVersion,
            providerSequence,
            durableBatchId,
            eventCount,
            sourceCount,
            payloadValueCount,
            inputChecksum,
            deltaId,
            values,
            schemaVersion,
            copyValues: true)
    {
    }

    private RadarProcessingHandlerDelta(
        string handlerName,
        string handlerContractVersion,
        RadarProcessingQueuedBatchSequence providerSequence,
        RadarProcessingDurableBatchId? durableBatchId,
        int eventCount,
        int sourceCount,
        long payloadValueCount,
        long inputChecksum,
        RadarProcessingHandlerDeltaId deltaId,
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values,
        int schemaVersion,
        bool copyValues)
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
        this.values = CopyValues(values, sourceCount, copyValues);
    }

    /// <summary>
    /// Handler name that produced the delta.
    /// </summary>
    public string HandlerName { get; }

    /// <summary>
    /// Handler contract version that produced the delta.
    /// </summary>
    public string HandlerContractVersion { get; }

    /// <summary>
    /// Provider sequence for the batch represented by the delta.
    /// </summary>
    public RadarProcessingQueuedBatchSequence ProviderSequence { get; }

    /// <summary>
    /// Durable batch id when the delta was produced from durable processing.
    /// </summary>
    public RadarProcessingDurableBatchId? DurableBatchId { get; }

    /// <summary>
    /// Number of events in the input batch.
    /// </summary>
    public int EventCount { get; }

    /// <summary>
    /// Source count expected by the delta values.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Payload value count for the input batch.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Deterministic checksum over the input batch identity.
    /// </summary>
    public long InputChecksum { get; }

    /// <summary>
    /// Stable identity derived from handler and batch identity.
    /// </summary>
    public RadarProcessingHandlerDeltaId DeltaId { get; }

    /// <summary>
    /// Handler delta schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Values carried by the delta.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerDeltaValue> Values => values;

    /// <summary>
    /// Creates a handler delta and derives the matching delta id.
    /// </summary>
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

    /// <summary>
    /// Creates a handler delta that takes ownership of precomputed value groups.
    /// </summary>
    public static RadarProcessingHandlerDelta CreateWithOwnedValues(
        string handlerName,
        string handlerContractVersion,
        RadarProcessingQueuedBatchSequence providerSequence,
        RadarProcessingDurableBatchId? durableBatchId,
        int eventCount,
        int sourceCount,
        long payloadValueCount,
        long inputChecksum,
        RadarProcessingHandlerDeltaValue[]? values = null) =>
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
            values,
            CurrentSchemaVersion,
            copyValues: false);

    /// <summary>
    /// Creates the stable delta id for the supplied handler and batch identity.
    /// </summary>
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
        int sourceCount,
        bool copyValues)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<RadarProcessingHandlerDeltaValue>();
        }

        var result = copyValues
            ? new RadarProcessingHandlerDeltaValue[values.Count]
            : values as RadarProcessingHandlerDeltaValue[] ??
              throw new ArgumentException(
                  "Owned handler delta values must be provided as an array.",
                  nameof(values));
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

            if (copyValues)
            {
                result[i] = value;
            }
        }

        return Array.AsReadOnly(result);
    }
}
