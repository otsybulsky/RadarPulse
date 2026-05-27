using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingCoreGuardrailTests
{
    [Fact]
    public void OwnedAndLeasedEquivalentBatchesProduceIdenticalSequentialResults()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var ownedCore = new RadarProcessingCore(universe);
        var leasedCore = new RadarProcessingCore(universe);
        var ownedBatch = CreateBatchBuilder(sourceCount: 2).Build();

        var ownedResult = ownedCore.Process(ownedBatch);
        RadarProcessingResult? leasedResult = null;
        CreateBatchBuilder(sourceCount: 2).ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            leasedResult = leasedCore.Process(batch);
        });

        Assert.NotNull(leasedResult);
        Assert.Equal(ownedResult.Metrics, leasedResult.Metrics);
        Assert.Equal(ownedResult.Validation, leasedResult.Validation);
        Assert.Equal(ownedCore.CreateSourceSnapshots(), leasedCore.CreateSourceSnapshots());
    }

    [Fact]
    public void ResultAndSnapshotsRemainStableAfterLeasedBuffersAreReused()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 4);
        AddEvent(
            builder,
            sourceId: 0,
            messageTimestampUtcTicks: 100,
            payload: new byte[] { 1, 2, 3, 4 });

        RadarProcessingResult? result = null;
        RadarSourceProcessingSnapshot snapshot = default;
        builder.ConsumeLeased(batch =>
        {
            result = core.Process(batch);
            snapshot = core.GetSourceSnapshot(sourceId: 0);
        });

        AddEvent(
            builder,
            sourceId: 0,
            messageTimestampUtcTicks: 101,
            payload: new byte[] { 100, 100, 100, 100 });
        var reusedBatch = builder.Build();

        Assert.NotNull(result);
        Assert.Equal(10, result.Metrics.RawValueChecksum);
        Assert.Equal(10, snapshot.RawValueChecksum);
        Assert.Equal(400, RadarProcessingPayloadReader.ComputeBatchMetrics(reusedBatch).RawValueChecksum);
        Assert.Equal(10, result.Metrics.RawValueChecksum);
        Assert.Equal(10, snapshot.RawValueChecksum);
    }

    [Fact]
    public void SequentialCountersMatchRadarEventBatchMetricsForOwnedBatch()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatchBuilder(sourceCount: 2).Build();
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Metrics.ProcessedBatchCount);
        Assert.Equal(batchMetrics.EventCount, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(batchMetrics.PayloadValueCount, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, result.Metrics.RawValueChecksum);
    }

    [Fact]
    public void InvalidProcessingDoesNotIncrementProcessedBatchCount()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var validBatch = CreateSingleEventBatch(
            sourceId: 0,
            messageTimestampUtcTicks: 100,
            payload: new byte[] { 1, 2, 3, 4 });
        var invalidBatch = CreateSingleEventBatch(
            sourceId: 1,
            messageTimestampUtcTicks: 101,
            payload: new byte[] { 5, 6, 7, 8 });

        var validResult = core.Process(validBatch);
        var invalidResult = core.Process(invalidBatch);

        Assert.True(validResult.IsValid);
        Assert.False(invalidResult.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, invalidResult.Validation.Error);
        Assert.Equal(1, invalidResult.Metrics.ProcessedBatchCount);
        Assert.Equal(1, invalidResult.Metrics.ProcessedStreamEventCount);
        Assert.Equal(10, invalidResult.Metrics.RawValueChecksum);
        Assert.Equal(invalidResult.Metrics, core.CreateMetrics());
    }

    [Fact]
    public void InvalidProcessingDoesNotMutateStateBeforeSourceValidationPasses()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var invalidBatch = CreateSingleEventBatch(
            sourceId: 1,
            messageTimestampUtcTicks: 100,
            payload: new byte[] { 1, 2, 3, 4 });

        var result = core.Process(invalidBatch);
        var snapshot = core.GetSourceSnapshot(sourceId: 0);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
        Assert.False(snapshot.IsActive);
        Assert.Equal(0, snapshot.ProcessedEventCount);
        Assert.Equal(0, snapshot.RawValueChecksum);
    }

    private static RadarEventBatch CreateSingleEventBatch(
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(
            builder,
            sourceId,
            messageTimestampUtcTicks,
            payload);
        return builder.Build();
    }

    private static RadarEventBatchBuilder CreateBatchBuilder(int sourceCount)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: sourceCount, initialPayloadCapacity: 8);
        AddEvent(
            builder,
            sourceId: 0,
            messageTimestampUtcTicks: 100,
            payload: new byte[] { 1, 2, 3, 4 });
        AddEvent(
            builder,
            sourceId: 1,
            messageTimestampUtcTicks: 101,
            payload: new byte[] { 0, 5, 1, 0 },
            wordSize: RadarStreamWordSize.SixteenBit);
        return builder;
    }

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        builder.AddEvent(
            CreateIdentity(sourceId),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)(wordSize == RadarStreamWordSize.EightBit
                ? payload.Length
                : payload.Length / sizeof(ushort)),
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

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
}
