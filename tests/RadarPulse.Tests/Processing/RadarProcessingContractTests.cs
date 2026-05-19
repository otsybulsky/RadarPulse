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
        Assert.Same(RadarProcessingAsyncExecutionOptions.Default, options.AsyncExecution);
    }

    [Fact]
    public void ExecutionModeEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingExecutionMode.Sequential);
        Assert.Equal(2, (int)RadarProcessingExecutionMode.PartitionedBarrier);
        Assert.Equal(3, (int)RadarProcessingExecutionMode.AsyncShardTransport);
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
    public void AsyncExecutionOptionsUseConservativeBorrowedBatchDefaults()
    {
        var options = RadarProcessingAsyncExecutionOptions.Default;

        Assert.Equal(1, options.WorkerCount);
        Assert.Equal(1, options.QueueCapacity);
        Assert.Equal(RadarProcessingWorkerAffinity.Shard, options.WorkerAffinity);
        Assert.Equal(RadarProcessingWorkerTimeoutPolicy.Disabled, options.TimeoutPolicy);
        Assert.Null(options.BatchTimeout);
        Assert.False(options.HasBatchTimeout);
    }

    [Fact]
    public void AsyncExecutionEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingWorkerAffinity.None);
        Assert.Equal(1, (int)RadarProcessingWorkerAffinity.Shard);

        Assert.Equal(0, (int)RadarProcessingWorkerTimeoutPolicy.Disabled);
        Assert.Equal(1, (int)RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy);
        Assert.Equal(2, (int)RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy);
    }

    [Fact]
    public void AsyncExecutionOptionsComposeExplicitWorkerSettings()
    {
        var timeout = TimeSpan.FromMilliseconds(250);

        var options = new RadarProcessingAsyncExecutionOptions(
            workerCount: 4,
            queueCapacity: 2,
            workerAffinity: RadarProcessingWorkerAffinity.None,
            timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy,
            batchTimeout: timeout);

        Assert.Equal(4, options.WorkerCount);
        Assert.Equal(2, options.QueueCapacity);
        Assert.Equal(RadarProcessingWorkerAffinity.None, options.WorkerAffinity);
        Assert.Equal(RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy, options.TimeoutPolicy);
        Assert.Equal(timeout, options.BatchTimeout);
        Assert.True(options.HasBatchTimeout);
    }

    [Fact]
    public void AsyncExecutionOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncExecutionOptions(workerCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncExecutionOptions(queueCapacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncExecutionOptions(workerAffinity: (RadarProcessingWorkerAffinity)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncExecutionOptions(timeoutPolicy: (RadarProcessingWorkerTimeoutPolicy)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncExecutionOptions(
                timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy,
                batchTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncExecutionOptions(batchTimeout: TimeSpan.FromMilliseconds(1)));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncExecutionOptions(
                timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy));
    }

    [Fact]
    public void CoreOptionsCarryAsyncExecutionOptionsWithoutChangingSynchronousMode()
    {
        var asyncExecution = new RadarProcessingAsyncExecutionOptions(
            workerCount: 2,
            queueCapacity: 1);

        var options = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 4,
            shardCount: 2,
            asyncExecution: asyncExecution);

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, options.ExecutionMode);
        Assert.Same(asyncExecution, options.AsyncExecution);
        Assert.Equal(4, options.PartitionCount);
        Assert.Equal(2, options.ShardCount);
    }

    [Fact]
    public void AsyncShardTransportModeIsRecognizedButNotExecutedBeforeRuntimeSlice()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 1,
            rangeBandCount: 1);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(RadarProcessingExecutionMode.AsyncShardTransport));
        var batch = new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var exception = Assert.Throws<NotSupportedException>(() => core.Process(batch));

        Assert.Contains("Async shard transport", exception.Message, StringComparison.Ordinal);
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
