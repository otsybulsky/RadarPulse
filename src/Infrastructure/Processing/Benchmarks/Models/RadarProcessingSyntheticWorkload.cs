using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Deterministic synthetic batch set used by processing benchmarks.
/// </summary>
public sealed class RadarProcessingSyntheticWorkload
{
    private readonly IReadOnlyList<RadarEventBatch> batches;

    private RadarProcessingSyntheticWorkload(
        RadarSourceUniverse sourceUniverse,
        RadarEventBatch[] batches,
        long eventsPerIteration,
        long payloadValuesPerIteration,
        long rawValueChecksumPerIteration)
    {
        SourceUniverse = sourceUniverse;
        this.batches = Array.AsReadOnly((RadarEventBatch[])batches.Clone());
        EventsPerIteration = eventsPerIteration;
        PayloadValuesPerIteration = payloadValuesPerIteration;
        RawValueChecksumPerIteration = rawValueChecksumPerIteration;
    }

    /// <summary>
    /// Source universe used to create all synthetic stream identities.
    /// </summary>
    public RadarSourceUniverse SourceUniverse { get; }

    /// <summary>
    /// Batches processed once per benchmark iteration.
    /// </summary>
    public IReadOnlyList<RadarEventBatch> Batches => batches;

    /// <summary>
    /// Batch count processed per iteration.
    /// </summary>
    public long BatchesPerIteration => batches.Count;

    /// <summary>
    /// Event count processed per iteration.
    /// </summary>
    public long EventsPerIteration { get; }

    /// <summary>
    /// Payload value count processed per iteration.
    /// </summary>
    public long PayloadValuesPerIteration { get; }

    /// <summary>
    /// Raw value checksum total per iteration.
    /// </summary>
    public long RawValueChecksumPerIteration { get; }

    /// <summary>
    /// Creates a deterministic synthetic workload from the supplied dimensions.
    /// </summary>
    public static RadarProcessingSyntheticWorkload Create(
        RadarProcessingSyntheticWorkloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var sourceUniverse = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: options.SourceCount,
            rangeBandCount: 1);
        var batches = new RadarEventBatch[options.BatchCount];
        var eventsPerIteration = 0L;
        var payloadValuesPerIteration = 0L;
        var rawValueChecksumPerIteration = 0L;

        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            var batch = CreateBatch(sourceUniverse, options, batchIndex);
            var metrics = RadarEventBatchMetrics.Compute(batch);

            batches[batchIndex] = batch;
            eventsPerIteration = checked(eventsPerIteration + metrics.EventCount);
            payloadValuesPerIteration = checked(payloadValuesPerIteration + metrics.PayloadValueCount);
            rawValueChecksumPerIteration = checked(rawValueChecksumPerIteration + metrics.RawValueChecksum);
        }

        return new RadarProcessingSyntheticWorkload(
            sourceUniverse,
            batches,
            eventsPerIteration,
            payloadValuesPerIteration,
            rawValueChecksumPerIteration);
    }

    private static RadarEventBatch CreateBatch(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingSyntheticWorkloadOptions options,
        int batchIndex)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: options.EventsPerBatch,
            initialPayloadCapacity: checked(options.EventsPerBatch * options.PayloadValuesPerEvent));

        for (var eventIndex = 0; eventIndex < options.EventsPerBatch; eventIndex++)
        {
            var globalEventIndex = checked((batchIndex * options.EventsPerBatch) + eventIndex);
            var sourceId = globalEventIndex % options.SourceCount;
            builder.AddEvent(
                CreateIdentity(sourceId, sourceUniverse.Version),
                volumeTimestampUtcTicks: 1_000,
                messageTimestampUtcTicks: 10_000 + sourceId,
                sourceRecord: batchIndex + 1,
                sourceMessage: eventIndex + 1,
                radialSequence: globalEventIndex,
                gateStart: 0,
                gateCount: (ushort)options.PayloadValuesPerEvent,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: CreatePayload(sourceId, batchIndex, eventIndex, options.PayloadValuesPerEvent));
        }

        return builder.Build();
    }

    private static RadarStreamIdentity CreateIdentity(
        int sourceId,
        SourceUniverseVersion sourceUniverseVersion) =>
        new(
            sourceId,
            radarOrdinal: 0,
            momentId: 0,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            dictionaryVersion: DictionaryVersion.Initial,
            sourceUniverseVersion: sourceUniverseVersion);

    private static byte[] CreatePayload(
        int sourceId,
        int batchIndex,
        int eventIndex,
        int payloadValuesPerEvent)
    {
        var payload = new byte[payloadValuesPerEvent];
        for (var valueIndex = 0; valueIndex < payload.Length; valueIndex++)
        {
            payload[valueIndex] = (byte)(1 + ((sourceId + batchIndex + eventIndex + valueIndex) % 251));
        }

        return payload;
    }
}
