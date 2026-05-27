using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingCoreSequentialTests
{
    [Fact]
    public void ConstructorRejectsInvalidInputsAndInvalidTopology()
    {
        var universe = CreateUniverse(sourceCount: 2);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingCore(null!));
        Assert.Throws<ArgumentException>(() => new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.Sequential,
                partitionCount: 3,
                shardCount: 1)));
    }

    [Fact]
    public void ConstructorAcceptsPartitionedBarrierMode()
    {
        var universe = CreateUniverse(sourceCount: 4);

        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, core.Options.ExecutionMode);
        Assert.Equal(4, core.Topology.PartitionCount);
        Assert.Equal(2, core.Topology.ShardCount);
    }

    [Fact]
    public void ProcessRejectsNullBatch()
    {
        var core = new RadarProcessingCore(CreateUniverse(sourceCount: 1));

        Assert.Throws<ArgumentNullException>(() => core.Process(null!));
    }

    [Fact]
    public void ProcessHonorsCancellationBeforeProcessing()
    {
        var core = new RadarProcessingCore(CreateUniverse(sourceCount: 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            core.Process(CreateEmptyBatch(core.Topology.SourceUniverseVersion), cancellation.Token));
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
    }

    [Fact]
    public void ProcessReturnsInvalidResultForUnsupportedStreamSchemaVersion()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateEmptyBatch(
            universe.Version,
            streamSchemaVersion: new StreamSchemaVersion(2));

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.UnsupportedStreamSchemaVersion, result.Validation.Error);
        Assert.Equal(-1, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceUniverseVersionMismatch()
    {
        var universe = CreateUniverse(sourceCount: 1, version: SourceUniverseVersion.Initial);
        var core = new RadarProcessingCore(universe);
        var batch = CreateEmptyBatch(new SourceUniverseVersion(2));

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceUniverseVersionMismatch, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void EmptyBatchProducesDeterministicZeroEventResult()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);

        var result = core.Process(CreateEmptyBatch(universe.Version));

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.Sequential, result.ExecutionMode);
        Assert.Equal(1, result.PartitionCount);
        Assert.Equal(1, result.ShardCount);
        Assert.Equal(1, result.Metrics.ProcessedBatchCount);
        Assert.Equal(0, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(0, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(0, result.Metrics.ActiveSourceCount);
        Assert.Equal(0, result.Metrics.RawValueChecksum);
        Assert.Equal(0UL, result.Metrics.ProcessingChecksum);
        Assert.Equal(result.Metrics, result.Validation.Metrics);
    }

    [Fact]
    public void SequentialProcessingUpdatesMetricsAndSourceSnapshots()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var events = new[]
        {
            CreateEvent(
                sourceId: 0,
                messageTimestampUtcTicks: 100,
                payloadOffset: 0,
                gateCount: 4,
                wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(
                sourceId: 1,
                messageTimestampUtcTicks: 101,
                payloadOffset: 4,
                gateCount: 2,
                wordSize: RadarStreamWordSize.SixteenBit)
        };
        var payload = new byte[] { 1, 2, 3, 4, 0, 5, 1, 0 };
        var batch = CreateBatch(universe.Version, events, payload);
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Metrics.ProcessedBatchCount);
        Assert.Equal(batchMetrics.EventCount, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(batchMetrics.PayloadValueCount, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, result.Metrics.RawValueChecksum);
        Assert.Equal(2, result.Metrics.ActiveSourceCount);
        Assert.NotEqual(0UL, result.Metrics.ProcessingChecksum);
        Assert.Equal(result.Metrics, core.CreateMetrics());

        var sourceZero = core.GetSourceSnapshot(sourceId: 0);
        Assert.True(sourceZero.IsActive);
        Assert.Equal(1, sourceZero.ProcessedEventCount);
        Assert.Equal(4, sourceZero.ProcessedPayloadValueCount);
        Assert.Equal(10, sourceZero.RawValueChecksum);
        Assert.Equal(100, sourceZero.LastMessageTimestampUtcTicks);
        Assert.NotEqual(0UL, sourceZero.ProcessingChecksum);

        var sourceOne = core.GetSourceSnapshot(sourceId: 1);
        Assert.True(sourceOne.IsActive);
        Assert.Equal(1, sourceOne.ProcessedEventCount);
        Assert.Equal(2, sourceOne.ProcessedPayloadValueCount);
        Assert.Equal(261, sourceOne.RawValueChecksum);
        Assert.Equal(101, sourceOne.LastMessageTimestampUtcTicks);
        Assert.NotEqual(0UL, sourceOne.ProcessingChecksum);
    }

    [Fact]
    public void SequentialProcessingAccumulatesAcrossBatches()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var first = CreateBatch(
            universe.Version,
            new[] { CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0) },
            new byte[] { 1, 2, 3, 4 });
        var second = CreateBatch(
            universe.Version,
            new[] { CreateEvent(sourceId: 0, messageTimestampUtcTicks: 101, payloadOffset: 0) },
            new byte[] { 5, 6, 7, 8 });

        core.Process(first);
        var result = core.Process(second);

        Assert.Equal(2, result.Metrics.ProcessedBatchCount);
        Assert.Equal(2, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(8, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(36, result.Metrics.RawValueChecksum);
        Assert.Equal(1, result.Metrics.ActiveSourceCount);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceIdOutsideUniverse()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(
            universe.Version,
            new[] { CreateEvent(sourceId: 2, messageTimestampUtcTicks: 100, payloadOffset: 0) },
            new byte[] { 1, 2, 3, 4 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.Validation.Error);
        Assert.Equal(2, result.Validation.SourceId);
        Assert.Equal(0, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceOwnershipMismatch()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(
                    sourceId: 0,
                    messageTimestampUtcTicks: 100,
                    payloadOffset: 0,
                    azimuthBucket: 1)
            },
            new byte[] { 1, 2, 3, 4 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOwnershipMismatch, result.Validation.Error);
        Assert.Equal(0, result.Validation.SourceId);
        Assert.Equal(0, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceLocalTimestampRegression()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 99, payloadOffset: 4)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.Validation.Error);
        Assert.Equal(0, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(0, result.Metrics.ProcessedBatchCount);
        Assert.Equal(1, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(4, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(10, result.Metrics.RawValueChecksum);
    }

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount,
        SourceUniverseVersion? version = null) =>
        new(
            version ?? SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion,
        StreamSchemaVersion? streamSchemaVersion = null) =>
        CreateBatch(
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>(),
            streamSchemaVersion);

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
