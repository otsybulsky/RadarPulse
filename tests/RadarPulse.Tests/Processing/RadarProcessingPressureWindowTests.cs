using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPressureWindowTests
{
    [Fact]
    public void SingleSpikeDoesNotMakeWindowEligibleBeforeMinimumSampleCount()
    {
        var window = new RadarProcessingPressureWindow(CreateWindowOptions());

        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));

        Assert.False(window.IsRebalanceEligible);
        Assert.Equal(1, window.SampleCount);
        Assert.Equal(RadarProcessingPressureBand.Hot, window.GetShard(0).Band);
    }

    [Fact]
    public void SustainedPressureEntersHotBand()
    {
        var window = new RadarProcessingPressureWindow(CreateWindowOptions());

        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));
        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));

        Assert.True(window.IsRebalanceEligible);
        Assert.Equal(2, window.SampleCount);
        Assert.Equal(RadarProcessingPressureBand.Hot, window.GetShard(0).Band);
        Assert.True(window.GetShard(0).IsHot);
        Assert.Equal(4.0, window.GetShard(0).AverageScore.Value);
    }

    [Fact]
    public void PressureBetweenHotEnterAndExitPreservesHotBand()
    {
        var window = new RadarProcessingPressureWindow(CreateWindowOptions());

        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));
        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));
        window.AddSample(CreateSample(sourceIds: [0, 0]));

        var shard = window.GetShard(0);
        Assert.Equal(2, window.SampleCount);
        Assert.Equal(3.0, shard.AverageScore.Value);
        Assert.Equal(RadarProcessingPressureBand.Hot, shard.Band);
    }

    [Fact]
    public void PressureBelowHotExitLeavesHotBand()
    {
        var window = new RadarProcessingPressureWindow(CreateWindowOptions());

        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));
        window.AddSample(CreateSample(sourceIds: [0, 0, 0, 0]));
        window.AddSample(CreateSample(sourceIds: [0, 0]));
        window.AddSample(CreateSample(sourceIds: []));

        var shard = window.GetShard(0);
        Assert.Equal(2, window.SampleCount);
        Assert.Equal(1.0, shard.AverageScore.Value);
        Assert.Equal(RadarProcessingPressureBand.Warm, shard.Band);
        Assert.False(shard.IsHot);
    }

    [Fact]
    public void EmptySamplesKeepShardsCold()
    {
        var window = new RadarProcessingPressureWindow(CreateWindowOptions());

        window.AddSample(CreateSample(sourceIds: []));
        window.AddSample(CreateSample(sourceIds: []));

        Assert.True(window.IsRebalanceEligible);
        Assert.All(window.Shards, shard =>
        {
            Assert.Equal(RadarProcessingPressureBand.Cold, shard.Band);
            Assert.Equal(RadarProcessingPressureScore.Zero, shard.AverageScore);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, shard.TotalMetrics);
        });
    }

    [Fact]
    public void WindowTracksPartitionPressureByPartitionIdAndOwnerShard()
    {
        var window = new RadarProcessingPressureWindow(
            new RadarProcessingPressureWindowOptions(sampleCapacity: 4, minimumSampleCount: 1));

        window.AddSample(CreateSample(sourceIds: [2, 2, 0]));

        var partitionZero = window.GetPartition(0);
        var partitionTwo = window.GetPartition(2);

        Assert.Equal(0, partitionZero.ShardId);
        Assert.Equal(1, partitionZero.TotalMetrics.EventCount);
        Assert.Equal(1, partitionTwo.ShardId);
        Assert.Equal(2, partitionTwo.TotalMetrics.EventCount);
        Assert.Equal(2.0, partitionTwo.AverageScore.Value);
    }

    [Fact]
    public void WindowCarriesLatestTopologyVersion()
    {
        var window = new RadarProcessingPressureWindow(
            new RadarProcessingPressureWindowOptions(sampleCapacity: 4, minimumSampleCount: 1));
        var sample = CreateSample(sourceIds: [1]);

        window.AddSample(sample);

        Assert.True(window.HasSamples);
        Assert.Equal(sample.TopologyVersion, window.LatestTopologyVersion);
    }

    [Fact]
    public void WindowRejectsMismatchedSampleShape()
    {
        var window = new RadarProcessingPressureWindow(CreateWindowOptions());

        window.AddSample(CreateSample(sourceCount: 4, partitionCount: 4, shardCount: 2, sourceIds: [0]));

        Assert.Throws<ArgumentException>(() =>
            window.AddSample(CreateSample(sourceCount: 6, partitionCount: 6, shardCount: 3, sourceIds: [0])));
    }

    [Fact]
    public void WindowOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(sampleCapacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(sampleCapacity: 2, minimumSampleCount: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(coldThreshold: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(warmExitThreshold: 2, warmEnterThreshold: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(warmEnterThreshold: 3, hotExitThreshold: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(hotExitThreshold: 5, hotEnterThreshold: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(hotEnterThreshold: 7, superHotExitThreshold: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureWindowOptions(superHotExitThreshold: 9, superHotEnterThreshold: 8));
    }

    private static RadarProcessingPressureWindowOptions CreateWindowOptions() =>
        new(
            sampleCapacity: 2,
            minimumSampleCount: 2,
            coldThreshold: 0.0,
            warmExitThreshold: 1.0,
            warmEnterThreshold: 2.0,
            hotExitThreshold: 3.0,
            hotEnterThreshold: 4.0,
            superHotExitThreshold: 7.0,
            superHotEnterThreshold: 8.0);

    private static RadarProcessingPressureSample CreateSample(
        int[] sourceIds) =>
        CreateSample(
            sourceCount: 4,
            partitionCount: 4,
            shardCount: 2,
            sourceIds);

    private static RadarProcessingPressureSample CreateSample(
        int sourceCount,
        int partitionCount,
        int shardCount,
        int[] sourceIds)
    {
        var universe = CreateUniverse(sourceCount);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(
            core.Process(CreateEightBitBatch(universe.Version, sourceIds)).Telemetry);

        return RadarProcessingPressureSample.FromTelemetry(
            telemetry,
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0,
                coldThreshold: 0.0,
                warmThreshold: 2.0,
                hotThreshold: 4.0,
                superHotThreshold: 8.0));
    }

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
                payloadOffset: i);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset) =>
        new(
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
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: 1);
}
