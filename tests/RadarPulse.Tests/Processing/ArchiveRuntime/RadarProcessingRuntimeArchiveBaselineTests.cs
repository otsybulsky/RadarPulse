using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRuntimeArchiveBaselineTests
{
    [Fact]
    public void BaselineCreatesRolloutAsyncCoreOptions()
    {
        var options = RadarProcessingRuntimeArchiveBaseline.CreateCoreOptions(
            partitionCount: 8,
            shardCount: 4);

        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, options.ExecutionMode);
        Assert.Equal(8, options.PartitionCount);
        Assert.Equal(4, options.ShardCount);
        Assert.True(options.EnableValidation);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, options.AsyncExecution.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            options.AsyncExecution.QueueCapacity);
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(options));
    }

    [Fact]
    public void BaselineCreatesRolloutAsyncExecutionOptions()
    {
        var asyncExecution = RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution();

        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, asyncExecution.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            asyncExecution.QueueCapacity);
    }

    [Fact]
    public void BaselineExposesOrderedConcurrencyCapacityIndependentFromQueues()
    {
        var ordered = RadarProcessingRuntimeArchiveBaseline.OrderedConcurrencyOptions;
        var provider = RadarProcessingRuntimeArchiveBaseline.QueuedOverlapOptions;
        var asyncExecution = RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution();

        Assert.Equal(
            RadarProcessingOrderedConcurrencyOptions.DefaultActiveBatchCapacity,
            ordered.ActiveBatchCapacity);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            ordered.ActiveBatchCapacity);
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(ordered));
        Assert.NotEqual(provider.QueueOptions.Capacity, ordered.ActiveBatchCapacity);
        Assert.NotEqual(asyncExecution.QueueCapacity, ordered.ActiveBatchCapacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, provider.QueueOptions.Capacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity, asyncExecution.QueueCapacity);
    }

    [Fact]
    public void OrderedConcurrencyOptionsValidateActiveBatchCapacity()
    {
        var sequential = RadarProcessingOrderedConcurrencyOptions.Sequential;
        var explicitConcurrent = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2);

        Assert.Equal(1, sequential.ActiveBatchCapacity);
        Assert.True(sequential.IsSequential);
        Assert.Equal(2, explicitConcurrent.ActiveBatchCapacity);
        Assert.False(explicitConcurrent.IsSequential);
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(sequential));
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(explicitConcurrent));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingOrderedConcurrencyOptions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingOrderedConcurrencyOptions(-1));
    }

    [Fact]
    public void BaselineKeepsQueuedOverlapProviderDefaultSeparate()
    {
        var options = RadarProcessingRuntimeArchiveBaseline.QueuedOverlapOptions;

        Assert.Same(RadarProcessingArchiveQueuedOverlapOptions.Default, options);
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions(options));
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
            options.QueueOptions.Capacity);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            options.QueueOptions.MaxRetainedPayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
            options.RetainedPayloadOptions.Strategy);
        Assert.Equal(
            RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault,
            options.RetainedPayloadPrewarmOptions);
    }

    [Fact]
    public void ExplicitDiagnosticQueuedOverlapOptionsRemainOutsideBaseline()
    {
        var explicitOptions = new RadarProcessingArchiveQueuedOverlapOptions();

        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions(explicitOptions));
        Assert.Equal(RadarProcessingProviderQueueOptions.Default, explicitOptions.QueueOptions);
        Assert.Equal(RadarProcessingRetainedPayloadOptions.Default, explicitOptions.RetainedPayloadOptions);
        Assert.Equal(RadarProcessingRetainedPayloadPrewarmOptions.None, explicitOptions.RetainedPayloadPrewarmOptions);
    }

    [Fact]
    public void BaselineMatchRejectsNonRolloutExecutionShapes()
    {
        var sequential = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 8,
            shardCount: 4);
        var wrongWorkerCount = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 8,
            shardCount: 4,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 8));
        var wrongQueueCapacity = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 8,
            shardCount: 4,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 4, queueCapacity: 7));

        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(sequential));
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(wrongWorkerCount));
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(wrongQueueCapacity));
    }

    [Fact]
    public void BaselineCanCreateCoreForSuppliedUniverseWithoutChangingCoreDefault()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 8,
            rangeBandCount: 1);

        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 8,
            shardCount: 4);

        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(core.Options));
        Assert.Equal(RadarProcessingExecutionMode.Sequential, RadarProcessingCoreOptions.Default.ExecutionMode);
    }

    [Fact]
    public void BaselineCanCreateRebalanceSessionForSuppliedUniverse()
    {
        var universe = CreateUniverse(sourceCount: 8);

        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);

        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(session.Core.Options));
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, session.Core.Options.ExecutionMode);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            session.Core.Options.AsyncExecution.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            session.Core.Options.AsyncExecution.QueueCapacity);
        Assert.Equal(8, session.PolicyState.PartitionCount);
        Assert.Equal(4, session.PolicyState.ShardCount);
    }

    [Fact]
    public async Task BaselineRebalanceSessionComposesWithOmittedQueuedOverlapDefault()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 1, 2, 3]),
                    cancellationToken);
                return CreatePublishResult(
                    universe,
                    batchCount: 1,
                    eventCount: 4,
                    payloadBytes: 4);
            },
            session);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        var processing = Assert.Single(result.Consumer.SessionResult.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
        Assert.Equal(8, processing.ProcessingResult?.PartitionCount);
        Assert.Equal(4, processing.ProcessingResult?.ShardCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
        Assert.True(processing.RebalanceResult?.Validation.IsValid);
    }

    [Fact]
    public async Task SuppliedRebalanceSessionKeepsExplicitExecutionMode()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));
        var session = new RadarProcessingRebalanceSession(core);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16));

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 1]),
                    cancellationToken);
                return CreatePublishResult(
                    universe,
                    batchCount: 1,
                    eventCount: 2,
                    payloadBytes: 2);
            },
            session,
            options);

        Assert.True(result.IsCompleted);
        var processing = Assert.Single(result.Consumer.SessionResult.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, processing.ProcessingResult?.ExecutionMode);
        Assert.Null(processing.RebalanceResult?.WorkerTelemetry);
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(session.Core.Options));
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
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 100 + i,
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

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        RadarSourceUniverse universe,
        long batchCount,
        long eventCount,
        long payloadBytes)
    {
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        return new ArchiveRadarEventBatchPublishResult(
            FilePath: "synthetic",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: payloadBytes,
            CompressedRecordCount: checked((int)batchCount),
            CompressedBytes: payloadBytes,
            DecompressedBytes: payloadBytes,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: universe.Version,
            BatchCount: batchCount,
            EventCount: eventCount,
            PayloadBytes: payloadBytes,
            PayloadValueCount: payloadBytes,
            RawValueChecksum: 0,
            DictionarySnapshot: normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }
}
