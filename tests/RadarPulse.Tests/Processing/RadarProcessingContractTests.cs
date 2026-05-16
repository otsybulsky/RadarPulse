using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingContractTests
{
    [Fact]
    public void DefaultOptionsUseSequentialSinglePartitionSingleShard()
    {
        var options = RadarProcessingCoreOptions.Default;

        Assert.Equal(RadarProcessingExecutionMode.Sequential, options.ExecutionMode);
        Assert.Equal(1, options.PartitionCount);
        Assert.Equal(1, options.ShardCount);
        Assert.True(options.EnableValidation);
    }

    [Fact]
    public void OptionsRejectInvalidExecutionMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingCoreOptions((RadarProcessingExecutionMode)255));
    }

    [Fact]
    public void OptionsRejectInvalidTopologyCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingCoreOptions(partitionCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingCoreOptions(shardCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingCoreOptions(partitionCount: 1, shardCount: 2));
    }

    [Fact]
    public void ValidationResultCarriesValidMetrics()
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 2,
            ProcessedPayloadValueCount: 3,
            ActiveSourceCount: 1,
            RawValueChecksum: 4,
            ProcessingChecksum: 5);

        var result = RadarProcessingValidationResult.Valid(metrics);

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.None, result.Error);
        Assert.Equal(-1, result.SourceId);
        Assert.Equal(-1, result.EventIndex);
        Assert.Equal(string.Empty, result.Message);
        Assert.Equal(metrics, result.Metrics);
        Assert.Null(result.ExpectedMetrics);
    }

    [Fact]
    public void ValidationResultRejectsInvalidResultWithoutError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.None,
                sourceId: -1,
                eventIndex: -1,
                message: "missing error"));
    }

    [Fact]
    public void ValidationResultRejectsEmptyInvalidMessage()
    {
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingValidationResult.Invalid(
                RadarProcessingValidationError.MetricsMismatch,
                sourceId: -1,
                eventIndex: -1,
                message: string.Empty));
    }

    [Fact]
    public void ResultCarriesModeTopologyMetricsAndValidation()
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 3,
            ProcessedStreamEventCount: 5,
            ProcessedPayloadValueCount: 8,
            ActiveSourceCount: 2,
            RawValueChecksum: 13,
            ProcessingChecksum: 21);
        var validation = RadarProcessingValidationResult.Valid(metrics);

        var result = new RadarProcessingResult(
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 4,
            shardCount: 2,
            metrics,
            validation);

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.TopologyVersion);
        Assert.Equal(4, result.PartitionCount);
        Assert.Equal(2, result.ShardCount);
        Assert.Equal(metrics, result.Metrics);
        Assert.Same(validation, result.Validation);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyResultUsesOptionsAndEmptyMetrics()
    {
        var options = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 4,
            shardCount: 2);

        var result = RadarProcessingResult.Empty(options);

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.TopologyVersion);
        Assert.Equal(4, result.PartitionCount);
        Assert.Equal(2, result.ShardCount);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Validation.Metrics);
    }

    [Fact]
    public void ResultRejectsTelemetryTopologyVersionMismatch()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 1,
            rangeBandCount: 1);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 1,
                shardCount: 1));
        var batch = new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());
        var result = core.Process(batch);

        Assert.NotNull(result.Telemetry);
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingResult(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 1,
                shardCount: 1,
                result.Metrics,
                result.Validation,
                result.Telemetry,
                new RadarProcessingTopologyVersion(result.TopologyVersion.Value + 1)));
    }
}
