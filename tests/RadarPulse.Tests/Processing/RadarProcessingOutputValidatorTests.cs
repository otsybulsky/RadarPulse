using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingOutputValidatorTests
{
    [Fact]
    public void ValidSequentialOutputPasses()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(sourceIds: new[] { 0, 1 }, messageTimestampUtcTicks: new[] { 100L, 101L });
        var beforeSnapshots = core.CreateSourceSnapshots();
        var previousMetrics = core.CreateMetrics();

        var result = core.Process(batch);
        var afterSnapshots = core.CreateSourceSnapshots();

        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            result,
            beforeSnapshots,
            afterSnapshots,
            previousMetrics);

        Assert.True(validation.IsValid);
        Assert.Equal(result.Metrics, validation.Metrics);
    }

    [Fact]
    public void ValidPartitionedOutputPassesWithTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(sourceIds: new[] { 0, 2, 3 }, messageTimestampUtcTicks: new[] { 100L, 101L, 102L });
        var beforeSnapshots = core.CreateSourceSnapshots();
        var previousMetrics = core.CreateMetrics();

        var result = core.Process(batch);
        var afterSnapshots = core.CreateSourceSnapshots();

        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            result,
            beforeSnapshots,
            afterSnapshots,
            previousMetrics);

        Assert.True(validation.IsValid);
        Assert.NotNull(result.Telemetry);
        Assert.Equal(result.Metrics, validation.Metrics);
    }

    [Fact]
    public void ValidatorDetectsMissingProcessedEvent()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var batch = CreateBatch(sourceIds: new[] { 0, 1 }, messageTimestampUtcTicks: new[] { 100L, 101L });
        var beforeStore = new RadarSourceProcessingStateStore(universe);
        var afterStore = new RadarSourceProcessingStateStore(universe);
        ApplyEvent(afterStore, batch, eventIndex: 0);
        var actualMetrics = afterStore.CreateMetrics(processedBatchCount: 1);
        var result = CreateSequentialResult(actualMetrics);

        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            result,
            beforeStore.CreateSnapshots(),
            afterStore.CreateSnapshots(),
            beforeStore.CreateMetrics());

        Assert.False(validation.IsValid);
        Assert.Equal(RadarProcessingValidationError.MetricsMismatch, validation.Error);
        Assert.Equal(1, validation.SourceId);
        Assert.Equal(1, validation.Metrics.ProcessedStreamEventCount);
        Assert.NotNull(validation.ExpectedMetrics);
        Assert.Equal(2, validation.ExpectedMetrics.Value.ProcessedStreamEventCount);
    }

    [Fact]
    public void ValidatorDetectsDuplicateProcessedEvent()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var batch = CreateBatch(sourceIds: new[] { 0 }, messageTimestampUtcTicks: new[] { 100L });
        var beforeStore = new RadarSourceProcessingStateStore(universe);
        var afterStore = new RadarSourceProcessingStateStore(universe);
        ApplyEvent(afterStore, batch, eventIndex: 0);
        ApplyEvent(afterStore, batch, eventIndex: 0);
        var actualMetrics = afterStore.CreateMetrics(processedBatchCount: 1);
        var result = CreateSequentialResult(actualMetrics);

        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            result,
            beforeStore.CreateSnapshots(),
            afterStore.CreateSnapshots(),
            beforeStore.CreateMetrics());

        Assert.False(validation.IsValid);
        Assert.Equal(RadarProcessingValidationError.MetricsMismatch, validation.Error);
        Assert.Equal(0, validation.SourceId);
        Assert.Equal(2, validation.Metrics.ProcessedStreamEventCount);
        Assert.NotNull(validation.ExpectedMetrics);
        Assert.Equal(1, validation.ExpectedMetrics.Value.ProcessedStreamEventCount);
    }

    [Fact]
    public void ValidatorDetectsSourceLocalOrderViolation()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var beforeStore = new RadarSourceProcessingStateStore(universe);
        var seedBatch = CreateBatch(sourceIds: new[] { 0 }, messageTimestampUtcTicks: new[] { 200L });
        ApplyEvent(beforeStore, seedBatch, eventIndex: 0);
        var batch = CreateBatch(sourceIds: new[] { 0 }, messageTimestampUtcTicks: new[] { 199L });
        var previousMetrics = beforeStore.CreateMetrics(processedBatchCount: 1);
        var result = CreateSequentialResult(previousMetrics);

        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            result,
            beforeStore.CreateSnapshots(),
            beforeStore.CreateSnapshots(),
            previousMetrics);

        Assert.False(validation.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, validation.Error);
        Assert.Equal(0, validation.SourceId);
        Assert.Equal(0, validation.EventIndex);
    }

    [Fact]
    public void ValidatorRequiresTelemetryForValidPartitionedOutput()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreatePartitionedCore(universe, partitionCount: 2, shardCount: 1);
        var batch = CreateBatch(sourceIds: new[] { 0, 1 }, messageTimestampUtcTicks: new[] { 100L, 101L });
        var beforeSnapshots = core.CreateSourceSnapshots();
        var previousMetrics = core.CreateMetrics();

        var result = core.Process(batch);
        var afterSnapshots = core.CreateSourceSnapshots();
        var resultWithoutTelemetry = new RadarProcessingResult(
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 2,
            shardCount: 1,
            result.Metrics,
            RadarProcessingValidationResult.Valid(result.Metrics));

        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            resultWithoutTelemetry,
            beforeSnapshots,
            afterSnapshots,
            previousMetrics);

        Assert.False(validation.IsValid);
        Assert.Equal(RadarProcessingValidationError.MetricsMismatch, validation.Error);
        Assert.Contains("telemetry", validation.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RadarProcessingResult CreateSequentialResult(RadarProcessingMetrics metrics) =>
        new(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics));

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

    private static RadarEventBatch CreateBatch(
        int[] sourceIds,
        long[] messageTimestampUtcTicks)
    {
        Assert.Equal(sourceIds.Length, messageTimestampUtcTicks.Length);

        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: sourceIds.Length,
            initialPayloadCapacity: sourceIds.Length);
        for (var i = 0; i < sourceIds.Length; i++)
        {
            builder.AddEvent(
                CreateIdentity(sourceIds[i]),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampUtcTicks[i],
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: new byte[] { (byte)(i + 1) });
        }

        return builder.Build();
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

    private static void ApplyEvent(
        RadarSourceProcessingStateStore store,
        RadarEventBatch batch,
        int eventIndex)
    {
        var streamEvent = batch.Events.Span[eventIndex];
        var payloadMetrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, batch.Payload.Span);

        store.ApplyProcessedEvent(
            streamEvent,
            payloadMetrics.PayloadValueCount,
            payloadMetrics.RawValueChecksum);
    }
}
