using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingBatchDeltaTests
{
    [Fact]
    public void DeltaCommitMatchesPartitionedProcessingWithoutMutatingBeforeCommit()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var direct = CreateCore(universe);
        var deltaCore = CreateCore(universe);
        var batch = CreateBatch(universe.Version, [0, 1, 2, 3], messageTimestampBase: 100);

        var directResult = direct.Process(batch);
        using var delta = deltaCore.ComputeProcessingDelta(batch);

        Assert.Equal(RadarProcessingMetrics.Empty, deltaCore.CreateMetrics());

        var deltaResult = deltaCore.CommitProcessingDelta(delta);

        Assert.True(directResult.IsValid);
        Assert.True(deltaResult.IsValid);
        Assert.Equal(directResult.Metrics, deltaResult.Metrics);
        Assert.Equal(directResult.Telemetry?.BatchMetrics, deltaResult.Telemetry?.BatchMetrics);
        Assert.Equal(direct.CreateSourceSnapshots(), deltaCore.CreateSourceSnapshots());
    }

    [Fact]
    public void DeltaCommitDetectsPriorSourceOrderViolationWithoutPartialMutation()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe);
        var first = CreateBatch(universe.Version, [0], messageTimestampBase: 200);
        var outOfOrder = CreateBatch(universe.Version, [0], messageTimestampBase: 100);

        var firstResult = core.Process(first);
        var before = core.CreateSourceSnapshots();
        using var delta = core.ComputeProcessingDelta(outOfOrder);

        var result = core.CommitProcessingDelta(delta);

        Assert.True(firstResult.IsValid);
        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.Validation.Error);
        Assert.Equal(before, core.CreateSourceSnapshots());
        Assert.Equal(firstResult.Metrics, core.CreateMetrics());
    }

    [Fact]
    public void DeltaComputeRejectsIntraBatchSourceOrderViolationBeforeMutation()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe);
        var outOfOrder = CreateBatch(universe.Version, [0, 0], messageTimestampBase: 200, secondTimestampDelta: -100);

        var exception = Assert.Throws<InvalidOperationException>(() => core.ComputeProcessingDelta(outOfOrder));

        Assert.Contains("non-decreasing message timestamp", exception.Message, StringComparison.Ordinal);
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
    }

    [Fact]
    public void DeltaComputeRejectsHandlerCoreUntilHandlerDeltaContractExists()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 1,
                shardCount: 1,
                handlers: [new NoopHandler()]));

        var exception = Assert.Throws<NotSupportedException>(() =>
            core.ComputeProcessingDelta(CreateBatch(universe.Version, [0], messageTimestampBase: 100)));

        Assert.Contains("handler-free", exception.Message, StringComparison.Ordinal);
    }

    private static RadarProcessingCore CreateCore(RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        long messageTimestampBase,
        long secondTimestampDelta = 1)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            var timestamp = i == 1
                ? messageTimestampBase + secondTimestampDelta
                : messageTimestampBase + i;
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: timestamp,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceIds[i],
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: i,
                payloadLength: 1);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private sealed class NoopHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new("noop", int64SlotCount: 0, doubleSlotCount: 0);

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
        }
    }
}
