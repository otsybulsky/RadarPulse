using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProductionPipelineRunnerTests
{
    [Fact]
    public async Task PipelineRunnerProcessesBatchesAndPublishesBffRun()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var store = new RadarProcessingBffReadModelStore();
        var request = new RadarProcessingProductionPipelineRunRequest(
            "run-handler-free",
            universe,
            new[]
            {
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 200)
            },
            partitionCount: 2,
            shardCount: 2,
            readModelStore: store);
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);

        Assert.True(result.IsCompleted);
        Assert.True(result.OperatorSummary.IsReady);
        Assert.Equal(RadarProcessingProductionPipelineHandlerMode.HandlerFree, result.OperatorSummary.HandlerMode);
        Assert.NotNull(result.RuntimeResult);
        Assert.NotNull(result.ReadModel);
        Assert.Equal(1, store.Count);
        Assert.True(store.TryGetLatestRun(out var latest));
        Assert.Equal("run-handler-free", latest!.RunId);
        Assert.Equal(2, latest.Batches.Count);
        Assert.Equal(2, latest.Sources.Count);
        Assert.True(latest.Diagnostics.ProcessingCompletenessPassed);
        Assert.True(latest.Diagnostics.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineOptionSource.Profile,
            result.Configuration.ProviderMode.Source);
    }

    [Fact]
    public async Task MergeableHandlerRunUsesHandlerDeltaMergePosture()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "run-mergeable",
            universe,
            new[]
            {
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 200)
            },
            partitionCount: 2,
            shardCount: 2,
            handlers: RadarProcessingBenchmarkHandlers.Create(
                RadarProcessingBenchmarkHandlerSet.CounterChecksum));
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);

        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingProductionPipelineHandlerMode.MergeableDelta, result.OperatorSummary.HandlerMode);
        Assert.True(result.RuntimeResult!.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.True(result.ReadModel!.Diagnostics.UsesOrderedHandlerDeltaMerge);
        Assert.NotEmpty(result.ReadModel.Sources[0].HandlerValues);
    }

    [Fact]
    public async Task SnapshotOnlyHandlerRunUsesSequentialFallbackPosture()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "run-snapshot",
            universe,
            new[] { CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100) },
            partitionCount: 2,
            shardCount: 2,
            handlers: new IRadarSourceProcessingHandler[] { new CountingHandler() });
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);

        Assert.True(result.IsCompleted);
        Assert.Equal(
            RadarProcessingProductionPipelineHandlerMode.SnapshotSequential,
            result.OperatorSummary.HandlerMode);
        Assert.True(result.RuntimeResult!.Plan.UsedSequentialFallback);
        Assert.True(result.ReadModel!.Diagnostics.UsesSequentialHandlerFallback);
        Assert.Contains("sequential fallback", result.OperatorSummary.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedHandlerRunBlocksWithHandlerSpecificReason()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "run-unsupported",
            universe,
            new[] { CreateBatch(universe.Version, [0], messageTimestampBase: 100) },
            partitionCount: 1,
            shardCount: 1,
            handlers: new IRadarSourceProcessingHandler[] { new UnsupportedHandler() });
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);

        Assert.False(result.IsCompleted);
        Assert.False(result.HasReadModel);
        Assert.False(result.OperatorSummary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.ResolveHandlerPosture,
            result.OperatorSummary.FallbackRecommendation);
        Assert.Contains("Unsupported handler 'unsupported'", result.OperatorSummary.FirstBlockingReason);
        Assert.Equal(0, result.ReadModelStore.Count);
    }

    [Fact]
    public async Task InvalidConfigurationReturnsBlockedResultWithoutReadModel()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "run-invalid",
            universe,
            new[] { CreateBatch(universe.Version, [0], messageTimestampBase: 100) },
            partitionCount: 1,
            shardCount: 1,
            options: new RadarProcessingProductionPipelineOptions(workerCount: 0));
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);

        Assert.False(result.IsCompleted);
        Assert.False(result.HasReadModel);
        Assert.False(result.OperatorSummary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration,
            result.OperatorSummary.FallbackRecommendation);
        Assert.Equal(0, result.ReadModelStore.Count);
    }

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
        long messageTimestampBase)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: sourceIds.Length,
            initialPayloadCapacity: sourceIds.Length);
        for (var i = 0; i < sourceIds.Length; i++)
        {
            builder.AddEvent(
                new RadarStreamIdentity(
                    sourceIds[i],
                    radarOrdinal: 0,
                    momentId: 0,
                    elevationSlot: 0,
                    azimuthBucket: (ushort)sourceIds[i],
                    rangeBand: 0,
                    dictionaryVersion: DictionaryVersion.Initial,
                    sourceUniverseVersion: sourceUniverseVersion),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
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

    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "counting",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }

    private sealed class UnsupportedHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "unsupported",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "unsupported.events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Unsupported;

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}
