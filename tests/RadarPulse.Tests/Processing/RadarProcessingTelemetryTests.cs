using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingTelemetryTests
{
    [Fact]
    public void PartitionedBarrierResultCarriesShardTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var events = new[]
        {
            CreateEvent(
                sourceId: 0,
                messageTimestampUtcTicks: 100,
                payloadOffset: 0,
                gateCount: 2,
                wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(
                sourceId: 2,
                messageTimestampUtcTicks: 101,
                payloadOffset: 2,
                gateCount: 1,
                wordSize: RadarStreamWordSize.SixteenBit),
            CreateEvent(
                sourceId: 3,
                messageTimestampUtcTicks: 102,
                payloadOffset: 4,
                gateCount: 3,
                wordSize: RadarStreamWordSize.EightBit)
        };
        var payload = new byte[] { 1, 2, 0, 5, 6, 7, 8 };
        var batch = CreateBatch(universe.Version, events, payload);
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        var result = core.Process(batch);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, telemetry.ExecutionMode);
        Assert.Equal(4, telemetry.PartitionCount);
        Assert.Equal(2, telemetry.ShardCount);
        Assert.Equal(batch.EventCount, telemetry.BatchMetrics.EventCount);
        Assert.Equal(batchMetrics.PayloadValueCount, telemetry.BatchMetrics.PayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, telemetry.BatchMetrics.RawValueChecksum);
        Assert.Equal(telemetry.BatchMetrics, SumPartitionMetrics(telemetry));
        Assert.Equal(telemetry.BatchMetrics, SumShardMetrics(telemetry));
        Assert.Equal(2, telemetry.Shards[0].PartitionCount);
        Assert.Equal(1, telemetry.Shards[0].ActivePartitionCount);
        Assert.Equal(2, telemetry.Shards[1].PartitionCount);
        Assert.Equal(2, telemetry.Shards[1].ActivePartitionCount);
    }

    [Fact]
    public void EmptyPartitionedBatchTelemetryIsStable()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var result = core.Process(batch);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingRouteMetrics.Empty, telemetry.BatchMetrics);
        Assert.Equal(-1, telemetry.HotPartitionId);
        Assert.Equal(-1, telemetry.HotShardId);

        foreach (var partition in telemetry.Partitions)
        {
            Assert.False(partition.HasWork);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, partition.Metrics);
        }

        foreach (var shard in telemetry.Shards)
        {
            Assert.False(shard.HasWork);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, shard.Metrics);
            Assert.Equal(0, shard.ActivePartitionCount);
        }
    }

    [Fact]
    public void TelemetryReportsHotShardAndPartitionDeterministically()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var hotBatch = CreateEightBitBatch(universe.Version, new[] { 2, 2, 0 });

        var hotTelemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(hotBatch).Telemetry);

        Assert.Equal(2, hotTelemetry.HotPartitionId);
        Assert.Equal(1, hotTelemetry.HotShardId);

        var tieCore = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var tieBatch = CreateEightBitBatch(universe.Version, new[] { 0, 2 });

        var tieTelemetry = Assert.IsType<RadarProcessingTelemetry>(tieCore.Process(tieBatch).Telemetry);

        Assert.Equal(0, tieTelemetry.HotPartitionId);
        Assert.Equal(0, tieTelemetry.HotShardId);
    }

    [Fact]
    public void PartitionedTelemetryDoesNotRetainLeasedPayloadReferences()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 4);
        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 100, payload: new byte[] { 1, 2, 3, 4 });
        RadarProcessingTelemetry? capturedTelemetry = null;

        builder.ConsumeLeased(batch =>
        {
            var result = core.Process(batch);
            capturedTelemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);
        });

        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 101, payload: new byte[] { 100, 101, 102, 103 });

        Assert.NotNull(capturedTelemetry);
        Assert.Equal(1, capturedTelemetry.BatchMetrics.EventCount);
        Assert.Equal(4, capturedTelemetry.BatchMetrics.PayloadValueCount);
        Assert.Equal(10, capturedTelemetry.BatchMetrics.RawValueChecksum);
        Assert.Equal(10, capturedTelemetry.Partitions[0].RawValueChecksum);
        Assert.Equal(10, capturedTelemetry.Shards[0].RawValueChecksum);
    }

    [Fact]
    public void SequentialResultDoesNotCarryPartitionedTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateEightBitBatch(universe.Version, new[] { 0 });

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.Sequential, result.ExecutionMode);
        Assert.Null(result.Telemetry);
    }

    private static RadarProcessingRouteMetrics SumPartitionMetrics(RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in telemetry.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in telemetry.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingCore CreatePartitionedCore(
        RadarSourceUniverse universe,
        int partitionCount,
        int shardCount) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(
                sourceIds[i],
                messageTimestampUtcTicks: 100 + i,
                payloadOffset: i,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit);
            payload[i] = (byte)(i + 1);
        }

        return CreateBatch(sourceUniverseVersion, events, payload);
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload)
    {
        builder.AddEvent(
            CreateIdentity(sourceId),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }

    private static RadarStreamIdentity CreateIdentity(int sourceId) =>
        new(
            sourceId,
            radarOrdinal: 0,
            momentId: 0,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            dictionaryVersion: DictionaryVersion.Initial,
            sourceUniverseVersion: SourceUniverseVersion.Initial);

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset,
        ushort gateCount,
        RadarStreamWordSize wordSize)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
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
