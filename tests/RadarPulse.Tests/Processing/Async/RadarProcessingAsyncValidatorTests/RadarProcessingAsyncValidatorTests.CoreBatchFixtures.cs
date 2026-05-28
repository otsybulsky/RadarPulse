using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                mode,
                partitionCount,
                shardCount,
                asyncExecution: asyncExecution));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static RadarEventBatch CreateMixedBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0, gateCount: 4),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 101, payloadOffset: 4, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 102, payloadOffset: 8, gateCount: 4),
                CreateEvent(sourceId: 5, messageTimestampUtcTicks: 103, payloadOffset: 12, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 104, payloadOffset: 16, gateCount: 4)
            },
            new byte[]
            {
                1, 2, 3, 4,
                0, 5, 1, 0,
                5, 6, 7, 8,
                2, 0, 0, 1,
                9, 10, 11, 12
            });

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset,
        ushort gateCount = 1,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: payloadOffset,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: gateCount,
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: payloadLength);
    }
}
