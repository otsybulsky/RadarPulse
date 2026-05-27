using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPressureSampleTests
{
    [Fact]
    public void EmptyTelemetryProducesZeroPressure()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        var sample = RadarProcessingPressureSample.FromTelemetry(telemetry);

        Assert.Equal(telemetry.TopologyVersion, sample.TopologyVersion);
        Assert.Equal(RadarProcessingRouteMetrics.Empty, sample.BatchMetrics);
        Assert.All(sample.Shards, shard =>
        {
            Assert.Equal(RadarProcessingPressureScore.Zero, shard.Score);
            Assert.Equal(RadarProcessingPressureBand.Cold, shard.Band);
        });
        Assert.All(sample.Partitions, partition =>
        {
            Assert.Equal(RadarProcessingPressureScore.Zero, partition.Score);
            Assert.Equal(RadarProcessingPressureBand.Cold, partition.Band);
        });
    }

    [Fact]
    public void PressureSampleCopiesTopologyVersionAndTelemetryMetrics()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateEightBitBatch(universe.Version, sourceIds: [0, 2, 2, 3]);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        var sample = RadarProcessingPressureSample.FromTelemetry(telemetry);

        Assert.Equal(telemetry.TopologyVersion, sample.TopologyVersion);
        Assert.Equal(telemetry.BatchMetrics, sample.BatchMetrics);
        Assert.Equal(telemetry.ShardCount, sample.ShardCount);
        Assert.Equal(telemetry.PartitionCount, sample.PartitionCount);

        for (var shardId = 0; shardId < telemetry.ShardCount; shardId++)
        {
            Assert.Equal(telemetry.Shards[shardId].ShardId, sample.Shards[shardId].ShardId);
            Assert.Equal(telemetry.Shards[shardId].Metrics, sample.Shards[shardId].Metrics);
            Assert.Equal(
                telemetry.Shards[shardId].ActivePartitionCount,
                sample.Shards[shardId].ActivePartitionCount);
        }

        for (var partitionId = 0; partitionId < telemetry.PartitionCount; partitionId++)
        {
            Assert.Equal(telemetry.Partitions[partitionId].PartitionId, sample.Partitions[partitionId].PartitionId);
            Assert.Equal(telemetry.Partitions[partitionId].ShardId, sample.Partitions[partitionId].ShardId);
            Assert.Equal(telemetry.Partitions[partitionId].Metrics, sample.Partitions[partitionId].Metrics);
        }
    }

    [Fact]
    public void PressureScoreIncreasesWithEventCount()
    {
        var options = new RadarProcessingPressureOptions(
            eventWeight: 2.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0);

        var lower = options.Score(new RadarProcessingRouteMetrics(1, payloadValueCount: 10, rawValueChecksum: 10));
        var higher = options.Score(new RadarProcessingRouteMetrics(2, payloadValueCount: 10, rawValueChecksum: 10));

        Assert.True(higher.Value > lower.Value);
        Assert.Equal(2.0, lower.Value);
        Assert.Equal(4.0, higher.Value);
    }

    [Fact]
    public void PressureScoreIncreasesWithPayloadValueCount()
    {
        var options = new RadarProcessingPressureOptions(
            eventWeight: 0.0,
            payloadValueWeight: 0.5,
            rawValueChecksumWeight: 0.0);

        var lower = options.Score(new RadarProcessingRouteMetrics(eventCount: 1, payloadValueCount: 4, rawValueChecksum: 10));
        var higher = options.Score(new RadarProcessingRouteMetrics(eventCount: 1, payloadValueCount: 8, rawValueChecksum: 10));

        Assert.True(higher.Value > lower.Value);
        Assert.Equal(2.0, lower.Value);
        Assert.Equal(4.0, higher.Value);
    }

    [Fact]
    public void PressureBandClassificationIsDeterministic()
    {
        var options = new RadarProcessingPressureOptions(
            eventWeight: 1.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0,
            coldThreshold: 0.0,
            warmThreshold: 10.0,
            hotThreshold: 50.0,
            superHotThreshold: 100.0);

        Assert.Equal(RadarProcessingPressureBand.Cold, options.Classify(new RadarProcessingPressureScore(0)));
        Assert.Equal(RadarProcessingPressureBand.Normal, options.Classify(new RadarProcessingPressureScore(5)));
        Assert.Equal(RadarProcessingPressureBand.Warm, options.Classify(new RadarProcessingPressureScore(10)));
        Assert.Equal(RadarProcessingPressureBand.Hot, options.Classify(new RadarProcessingPressureScore(50)));
        Assert.Equal(RadarProcessingPressureBand.SuperHot, options.Classify(new RadarProcessingPressureScore(100)));
    }

    [Fact]
    public void PressureSampleDoesNotRetainLeasedPayloadReferences()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 4);
        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 100, payload: [1, 2, 3, 4]);
        RadarProcessingPressureSample? capturedSample = null;

        builder.ConsumeLeased(batch =>
        {
            var result = core.Process(batch);
            var telemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);
            capturedSample = RadarProcessingPressureSample.FromTelemetry(telemetry);
        });

        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 101, payload: [100, 101, 102, 103]);

        Assert.NotNull(capturedSample);
        Assert.Equal(1, capturedSample.BatchMetrics.EventCount);
        Assert.Equal(4, capturedSample.BatchMetrics.PayloadValueCount);
        Assert.Equal(10, capturedSample.BatchMetrics.RawValueChecksum);
        Assert.Equal(10, capturedSample.Partitions[0].RawValueChecksum);
        Assert.Equal(10, capturedSample.Shards[0].RawValueChecksum);
    }

    [Fact]
    public void PressureSampleRemainsStableAfterFurtherProcessing()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreatePartitionedCore(universe, partitionCount: 2, shardCount: 1);
        var firstTelemetry = Assert.IsType<RadarProcessingTelemetry>(
            core.Process(CreateEightBitBatch(universe.Version, sourceIds: [0])).Telemetry);
        var sample = RadarProcessingPressureSample.FromTelemetry(firstTelemetry);

        core.Process(CreateEightBitBatch(universe.Version, sourceIds: [0, 1]));

        Assert.Equal(firstTelemetry.TopologyVersion, sample.TopologyVersion);
        Assert.Equal(firstTelemetry.BatchMetrics, sample.BatchMetrics);
        Assert.Equal(firstTelemetry.Shards[0].Metrics, sample.Shards[0].Metrics);
        Assert.Equal(firstTelemetry.Partitions[0].Metrics, sample.Partitions[0].Metrics);
        Assert.Equal(firstTelemetry.Partitions[1].Metrics, sample.Partitions[1].Metrics);
    }

    [Fact]
    public void PressureOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureScore(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureScore(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureScore(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureOptions(eventWeight: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureOptions(eventWeight: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(payloadValueWeight: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(coldThreshold: 10, warmThreshold: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(warmThreshold: 10, hotThreshold: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(hotThreshold: 10, superHotThreshold: 9));
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
