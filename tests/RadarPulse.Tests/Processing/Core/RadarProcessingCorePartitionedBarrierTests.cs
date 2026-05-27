using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingCorePartitionedBarrierTests
{
    [Fact]
    public void PartitionedBarrierMatchesSequentialMetricsAndSnapshots()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var sequential = new RadarProcessingCore(universe);
        var partitioned = CreatePartitionedCore(universe, partitionCount: 6, shardCount: 3);
        var batch = CreateMixedBatch();

        var sequentialResult = sequential.Process(batch);
        var partitionedResult = partitioned.Process(batch);

        Assert.True(sequentialResult.IsValid);
        Assert.True(partitionedResult.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, partitionedResult.ExecutionMode);
        Assert.Equal(sequentialResult.Metrics, partitionedResult.Metrics);
        Assert.Equal(sequential.CreateSourceSnapshots(), partitioned.CreateSourceSnapshots());
    }

    [Fact]
    public void PartitionedBarrierPreservesOwnedAndLeasedParity()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var ownedCore = CreatePartitionedCore(universe, partitionCount: 6, shardCount: 3);
        var leasedCore = CreatePartitionedCore(universe, partitionCount: 6, shardCount: 3);
        var ownedBatch = CreateMixedBatchBuilder().Build();

        var ownedResult = ownedCore.Process(ownedBatch);
        RadarProcessingResult? leasedResult = null;
        CreateMixedBatchBuilder().ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            leasedResult = leasedCore.Process(batch);
        });

        Assert.NotNull(leasedResult);
        Assert.Equal(ownedResult.Metrics, leasedResult.Metrics);
        Assert.Equal(ownedCore.CreateSourceSnapshots(), leasedCore.CreateSourceSnapshots());
    }

    [Fact]
    public void PartitionedBarrierPreservesSameSourceOrder()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 101, payloadOffset: 4),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 102, payloadOffset: 8),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 103, payloadOffset: 12)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var result = core.Process(batch);
        var sourceOne = core.GetSourceSnapshot(sourceId: 1);

        Assert.True(result.IsValid);
        Assert.Equal(3, sourceOne.ProcessedEventCount);
        Assert.Equal(103, sourceOne.LastMessageTimestampUtcTicks);
        Assert.Equal(110, sourceOne.RawValueChecksum);
    }

    [Fact]
    public void PartitionedBarrierReturnsInvalidResultForUnsupportedStreamSchemaVersion()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>(),
            streamSchemaVersion: new StreamSchemaVersion(2));

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.UnsupportedStreamSchemaVersion, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void PartitionedBarrierReturnsInvalidResultForSourceUniverseVersionMismatch()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var batch = CreateBatch(
            new SourceUniverseVersion(2),
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceUniverseVersionMismatch, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void PartitionedBarrierRejectsInvalidSourceBeforeStateMutation()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreatePartitionedCore(universe, partitionCount: 2, shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 2, messageTimestampUtcTicks: 101, payloadOffset: 4)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.Validation.Error);
        Assert.Equal(2, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
    }

    [Fact]
    public void PartitionedBarrierRejectsSourceOwnershipMismatchBeforeStateMutation()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreatePartitionedCore(universe, partitionCount: 2, shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(
                    sourceId: 0,
                    messageTimestampUtcTicks: 101,
                    payloadOffset: 4,
                    azimuthBucket: 1)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOwnershipMismatch, result.Validation.Error);
        Assert.Equal(0, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
    }

    [Fact]
    public void PartitionedBarrierReportsSourceLocalTimestampRegression()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreatePartitionedCore(universe, partitionCount: 2, shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 99, payloadOffset: 4)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.Validation.Error);
        Assert.Equal(1, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(0, result.Metrics.ProcessedBatchCount);
        Assert.Equal(1, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(10, result.Metrics.RawValueChecksum);
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

    private static RadarEventBatch CreateMixedBatch() =>
        CreateMixedBatchBuilder().Build();

    private static RadarEventBatchBuilder CreateMixedBatchBuilder()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 6, initialPayloadCapacity: 24);
        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 100, payload: new byte[] { 1, 2, 3, 4 });
        AddEvent(builder, sourceId: 3, messageTimestampUtcTicks: 101, payload: new byte[] { 0, 5, 1, 0 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceId: 1, messageTimestampUtcTicks: 102, payload: new byte[] { 5, 6, 7, 8 });
        AddEvent(builder, sourceId: 5, messageTimestampUtcTicks: 103, payload: new byte[] { 2, 0, 0, 1 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceId: 3, messageTimestampUtcTicks: 104, payload: new byte[] { 9, 10, 11, 12 });
        return builder;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload,
        StreamSchemaVersion? streamSchemaVersion = null) =>
        new(
            streamSchemaVersion ?? StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);

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
        ushort gateCount = 4,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit,
        ushort? azimuthBucket = null)
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
            azimuthBucket: azimuthBucket ?? (ushort)sourceId,
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
