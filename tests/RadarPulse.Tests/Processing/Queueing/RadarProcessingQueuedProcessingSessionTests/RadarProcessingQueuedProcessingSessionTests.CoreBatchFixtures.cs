using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProcessingSessionTests
{
    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode executionMode,
        int partitionCount = 1,
        int shardCount = 1,
        RadarProcessingAsyncExecutionOptions? asyncOptions = null,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                executionMode,
                partitionCount,
                shardCount,
                asyncExecution: asyncOptions,
                handlers: handlers));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId,
        long messageTimestampUtcTicks,
        byte firstPayloadValue)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        AddEvent(
            builder,
            sourceUniverseVersion,
            sourceId,
            messageTimestampUtcTicks,
            [firstPayloadValue, (byte)(firstPayloadValue + 1)]);
        return builder.Build();
    }

    private static RadarEventBatch CreateMixedBatch(
        SourceUniverseVersion sourceUniverseVersion)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 4, initialPayloadCapacity: 12);
        AddEvent(builder, sourceUniverseVersion, sourceId: 0, messageTimestampUtcTicks: 100, payload: [1, 2]);
        AddEvent(builder, sourceUniverseVersion, sourceId: 1, messageTimestampUtcTicks: 101, payload: [3, 4]);
        AddEvent(builder, sourceUniverseVersion, sourceId: 2, messageTimestampUtcTicks: 102, payload: [5, 6]);
        AddEvent(builder, sourceUniverseVersion, sourceId: 3, messageTimestampUtcTicks: 103, payload: [7, 8]);
        return builder.Build();
    }

    private static RadarEventBatch CreateInvalidSourceBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                new RadarStreamEvent(
                    sourceId: 1,
                    radarOrdinal: 0,
                    volumeTimestampUtcTicks: 90,
                    messageTimestampUtcTicks: 100,
                    sourceRecord: 0,
                    sourceMessage: 0,
                    radialSequence: 0,
                    elevationSlot: 0,
                    azimuthBucket: 1,
                    rangeBand: 0,
                    momentId: 0,
                    gateStart: 0,
                    gateCount: 2,
                    wordSize: RadarStreamWordSize.EightBit,
                    scale: 1.0f,
                    offset: 0.0f,
                    statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                    payloadOffset: 0,
                    payloadLength: 2)
            },
            new byte[] { 1, 2 });
}
