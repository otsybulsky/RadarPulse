using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class RadarEventBatchValidatorTests
{
    [Fact]
    public void ValidBatchReturnsDeterministicMetrics()
    {
        var universe = CreateUniverse();
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        var builder = new RadarEventBatchBuilder();

        builder.AddEvent(
            normalizer.Normalize("KTLX", "REF", elevationSlot: 0, azimuthBucket: 0, rangeBand: 0),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 10,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 2.0f,
            offset: 66.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [1, 2]);
        builder.AddEvent(
            normalizer.Normalize("KTLX", "VEL", elevationSlot: 0, azimuthBucket: 1, rangeBand: 0),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 11,
            sourceRecord: 0,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.SixteenBit,
            scale: 2.0f,
            offset: 129.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [0, 3, 0, 4]);

        var batch = builder.Build();
        var snapshot = normalizer.CreateDictionarySnapshot(batch.DictionaryVersion);

        var result = RadarEventBatchValidator.Validate(batch, universe, snapshot);

        Assert.True(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.None, result.Error);
        Assert.Equal(2, result.Metrics.EventCount);
        Assert.Equal(6, result.Metrics.PayloadBytes);
        Assert.Equal(4, result.Metrics.PayloadValueCount);
        Assert.Equal(10, result.Metrics.RawValueChecksum);
        Assert.NotEqual(0UL, result.Metrics.MetadataChecksum);
    }

    [Fact]
    public void ValidatorRejectsOutOfOrderEventChronology()
    {
        var universe = CreateUniverse();
        var events = new[]
        {
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 20, payloadOffset: 0),
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 19, payloadOffset: 2)
        };
        var batch = CreateBatch(events, [1, 2, 3, 4]);

        var result = RadarEventBatchValidator.Validate(batch, universe);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.ChronologyOrderViolation, result.Error);
        Assert.Equal(1, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsSourceIdOutsideSourceUniverse()
    {
        var universe = CreateUniverse();
        var batch = CreateBatch(
            [CreateEvent(sourceId: universe.SourceCount, messageTimestampUtcTicks: 10, payloadOffset: 0)],
            [1, 2]);

        var result = RadarEventBatchValidator.Validate(batch, universe);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.SourceIdOutsideUniverse, result.Error);
        Assert.Equal(0, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsSourceIdThatDoesNotMatchSourceDimensions()
    {
        var universe = CreateUniverse();
        var batch = CreateBatch(
            [CreateEvent(sourceId: 1, messageTimestampUtcTicks: 10, payloadOffset: 0)],
            [1, 2]);

        var result = RadarEventBatchValidator.Validate(batch, universe);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.SourceKeyMismatch, result.Error);
        Assert.Equal(0, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsNonContiguousPayloadReferences()
    {
        var universe = CreateUniverse();
        var events = new[]
        {
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 10, payloadOffset: 0),
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 11, payloadOffset: 0)
        };
        var batch = CreateBatch(events, [1, 2, 3, 4]);

        var result = RadarEventBatchValidator.Validate(batch, universe);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.PayloadReferenceNotContiguous, result.Error);
        Assert.Equal(1, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsPayloadBytesNotReferencedByEvents()
    {
        var universe = CreateUniverse();
        var batch = CreateBatch(
            [CreateEvent(sourceId: 0, messageTimestampUtcTicks: 10, payloadOffset: 0)],
            [1, 2, 3, 4]);

        var result = RadarEventBatchValidator.Validate(batch, universe);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.PayloadTailNotReferenced, result.Error);
        Assert.Equal(-1, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsDictionarySnapshotVersionMismatch()
    {
        var universe = CreateUniverse();
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        var identity = normalizer.Normalize("KTLX", "REF", elevationSlot: 0, azimuthBucket: 0, rangeBand: 0);
        var snapshot = normalizer.CreateDictionarySnapshot(identity.DictionaryVersion);
        var batch = CreateBatch(
            [CreateEvent(sourceId: 0, messageTimestampUtcTicks: 10, payloadOffset: 0)],
            [1, 2],
            dictionaryVersion: new DictionaryVersion(identity.DictionaryVersion.Value + 1));

        var result = RadarEventBatchValidator.Validate(batch, universe, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.DictionarySnapshotVersionMismatch, result.Error);
        Assert.Equal(-1, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsDictionaryIdsNotVisibleInSnapshot()
    {
        var universe = CreateUniverse();
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        var identity = normalizer.Normalize("KTLX", "REF", elevationSlot: 0, azimuthBucket: 0, rangeBand: 0);
        var snapshot = normalizer.CreateDictionarySnapshot(identity.DictionaryVersion);
        var batch = CreateBatch(
            [CreateEvent(sourceId: 0, momentId: 1, messageTimestampUtcTicks: 10, payloadOffset: 0)],
            [1, 2],
            dictionaryVersion: snapshot.Version);

        var result = RadarEventBatchValidator.Validate(batch, universe, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.MomentIdOutsideDictionary, result.Error);
        Assert.Equal(0, result.EventIndex);
    }

    [Fact]
    public void ValidatorRejectsExpectedMetricsMismatch()
    {
        var universe = CreateUniverse();
        var batch = CreateBatch(
            [CreateEvent(sourceId: 0, messageTimestampUtcTicks: 10, payloadOffset: 0)],
            [1, 2]);
        var expected = RadarEventBatchMetrics.Compute(batch) with
        {
            RawValueChecksum = 4
        };

        var result = RadarEventBatchValidator.Validate(batch, universe, expectedMetrics: expected);

        Assert.False(result.IsValid);
        Assert.Equal(RadarEventBatchValidationError.MetricsMismatch, result.Error);
        Assert.Equal(3, result.Metrics.RawValueChecksum);
        Assert.Equal(expected, result.ExpectedMetrics);
    }

    [Fact]
    public void DictionarySnapshotMetricsRemainStableForVersionedSnapshot()
    {
        var universe = CreateUniverse(radarOrdinalCount: 2);
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        var firstIdentity = normalizer.Normalize("KTLX", "REF", elevationSlot: 0, azimuthBucket: 0, rangeBand: 0);
        var firstSnapshot = normalizer.CreateDictionarySnapshot(firstIdentity.DictionaryVersion);
        var firstMetrics = RadarStreamDictionarySnapshotMetrics.Compute(firstSnapshot);

        normalizer.Normalize("KOUN", "VEL", elevationSlot: 0, azimuthBucket: 0, rangeBand: 0);
        var reconstructedFirstSnapshot = normalizer.CreateDictionarySnapshot(firstSnapshot.Version);
        var reconstructedFirstMetrics = RadarStreamDictionarySnapshotMetrics.Compute(reconstructedFirstSnapshot);
        var currentMetrics = RadarStreamDictionarySnapshotMetrics.Compute(normalizer.CreateDictionarySnapshot());

        Assert.Equal(firstMetrics, reconstructedFirstMetrics);
        Assert.NotEqual(firstMetrics.MappingChecksum, currentMetrics.MappingChecksum);
        Assert.Equal(1, firstMetrics.RadarCount);
        Assert.Equal(1, firstMetrics.MomentCount);
        Assert.Equal(2, currentMetrics.RadarCount);
        Assert.Equal(2, currentMetrics.MomentCount);
    }

    private static RadarSourceUniverse CreateUniverse(int radarOrdinalCount = 1) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount,
            elevationSlotCount: 2,
            azimuthBucketCount: 4,
            rangeBandCount: 2);

    private static RadarEventBatch CreateBatch(
        RadarStreamEvent[] events,
        byte[] payload,
        DictionaryVersion? dictionaryVersion = null) =>
        new(
            StreamSchemaVersion.Current,
            dictionaryVersion ?? new DictionaryVersion(3),
            SourceUniverseVersion.Initial,
            events,
            payload);

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset,
        ushort momentId = 0) =>
        new(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            elevationSlot: 0,
            azimuthBucket: 0,
            rangeBand: 0,
            momentId: momentId,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: 2);
}
