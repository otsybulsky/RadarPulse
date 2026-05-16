using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarSourceProcessingStateStoreTests
{
    [Fact]
    public void StateStoreIsSizedBySourceUniverse()
    {
        var universe = CreateUniverse(sourceCount: 5);

        var store = new RadarSourceProcessingStateStore(universe);

        Assert.Equal(universe.Version, store.SourceUniverseVersion);
        Assert.Equal(5, store.SourceCount);
        Assert.Equal(0, store.ActiveSourceCount);
        Assert.Equal(5, store.CreateSnapshots().Length);
    }

    [Fact]
    public void ApplyProcessedEventUpdatesOnlyTargetSource()
    {
        var store = CreateStore(sourceCount: 3);

        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 4,
            rawValueChecksum: 10);

        var untouched = store.GetSnapshot(sourceId: 0);
        var updated = store.GetSnapshot(sourceId: 1);
        var alsoUntouched = store.GetSnapshot(sourceId: 2);

        Assert.False(untouched.IsActive);
        Assert.Equal(0, untouched.ProcessedEventCount);
        Assert.True(updated.IsActive);
        Assert.Equal(1, updated.ProcessedEventCount);
        Assert.Equal(4, updated.ProcessedPayloadValueCount);
        Assert.Equal(10, updated.RawValueChecksum);
        Assert.Equal(100, updated.LastMessageTimestampUtcTicks);
        Assert.NotEqual(0UL, updated.ProcessingChecksum);
        Assert.False(alsoUntouched.IsActive);
        Assert.Equal(1, store.ActiveSourceCount);
    }

    [Fact]
    public void ActiveSourceCountCountsUniqueSources()
    {
        var store = CreateStore(sourceCount: 4);

        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 2, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 1,
            rawValueChecksum: 2);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 2, messageTimestampUtcTicks: 101),
            processedPayloadValueCount: 3,
            rawValueChecksum: 4);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 3, messageTimestampUtcTicks: 102),
            processedPayloadValueCount: 5,
            rawValueChecksum: 6);

        Assert.Equal(2, store.ActiveSourceCount);
        Assert.Equal(2, store.GetSnapshot(sourceId: 2).ProcessedEventCount);
        Assert.Equal(1, store.GetSnapshot(sourceId: 3).ProcessedEventCount);
    }

    [Fact]
    public void CreateSnapshotsReturnsReadSideProjection()
    {
        var store = CreateStore(sourceCount: 2);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 4,
            rawValueChecksum: 10);

        var firstProjection = store.CreateSnapshots();

        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 101),
            processedPayloadValueCount: 6,
            rawValueChecksum: 20);
        var secondProjection = store.CreateSnapshots();

        Assert.Equal(1, firstProjection[1].ProcessedEventCount);
        Assert.Equal(4, firstProjection[1].ProcessedPayloadValueCount);
        Assert.Equal(2, secondProjection[1].ProcessedEventCount);
        Assert.Equal(10, secondProjection[1].ProcessedPayloadValueCount);
    }

    [Fact]
    public void CreateMetricsAggregatesActiveSourceState()
    {
        var store = CreateStore(sourceCount: 3);

        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 4,
            rawValueChecksum: 10);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 2, messageTimestampUtcTicks: 101),
            processedPayloadValueCount: 6,
            rawValueChecksum: 20);

        var metrics = store.CreateMetrics(processedBatchCount: 7);

        Assert.Equal(7, metrics.ProcessedBatchCount);
        Assert.Equal(2, metrics.ProcessedStreamEventCount);
        Assert.Equal(10, metrics.ProcessedPayloadValueCount);
        Assert.Equal(2, metrics.ActiveSourceCount);
        Assert.Equal(30, metrics.RawValueChecksum);
        Assert.NotEqual(0UL, metrics.ProcessingChecksum);
    }

    [Fact]
    public void EmptyStoreCreatesEmptyMetrics()
    {
        var store = CreateStore(sourceCount: 3);

        var metrics = store.CreateMetrics();

        Assert.Equal(RadarProcessingMetrics.Empty, metrics);
    }

    [Fact]
    public void StateStoreRejectsInvalidInputs()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var store = new RadarSourceProcessingStateStore(universe);

        Assert.Throws<ArgumentNullException>(() => new RadarSourceProcessingStateStore(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.GetSnapshot(sourceId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.GetSnapshot(sourceId: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ApplyProcessedEvent(
            CreateEvent(sourceId: 2, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 1,
            rawValueChecksum: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: -1,
            rawValueChecksum: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 1,
            rawValueChecksum: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.CreateMetrics(processedBatchCount: -1));
    }

    [Fact]
    public void StateStoreRejectsSourceLocalTimestampRegression()
    {
        var store = CreateStore(sourceCount: 2);

        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 1,
            rawValueChecksum: 1);

        Assert.Throws<InvalidOperationException>(() => store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 99),
            processedPayloadValueCount: 1,
            rawValueChecksum: 1));
    }

    private static RadarSourceProcessingStateStore CreateStore(int sourceCount) =>
        new(CreateUniverse(sourceCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks) =>
        new(
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
            gateCount: 4,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: 0,
            payloadLength: 4);
}
