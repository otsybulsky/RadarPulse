using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductPipelineDtoTests
{
    [Fact]
    public async Task CompletedProductionPipelineResultMapsToProductRunDetail()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "product-run",
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
        var detail = RadarPulseProductPipelineMapper.ToProductRunDetail(
            result,
            new RadarPulseProductInputSummary(
                RadarPulseProductInputKind.Synthetic,
                "deterministic synthetic product run",
                "synthetic",
                BatchCount: 2,
                EventCount: 4));

        Assert.Equal("product-run", detail.RunId);
        Assert.True(detail.IsReady);
        Assert.True(detail.HasReadModel);
        Assert.Equal(RadarPulseProductRunState.Completed, detail.Summary.State);
        Assert.Equal(RadarPulseProductHandlerMode.MergeableDelta, detail.Summary.HandlerMode);
        Assert.Equal(RadarPulseProductFallbackRecommendation.None, detail.Summary.FallbackRecommendation);
        Assert.Equal(2, detail.Batches.Count);
        Assert.Equal(2, detail.Sources.Count);
        Assert.True(detail.Diagnostics!.ProcessingCompletenessPassed);
        Assert.True(detail.Diagnostics.UsesOrderedHandlerDeltaMerge);
        Assert.NotNull(detail.HandlerContract);
        Assert.NotEmpty(detail.HandlerContract!.Handlers);
        Assert.NotEmpty(detail.Sources[0].HandlerValues);
        Assert.Equal(2, detail.CapacityEvidence.AcceptedBatchCount);
        Assert.Equal(2, detail.CapacityEvidence.CommittedBatchCount);
    }

    [Fact]
    public async Task ProductConfigurationPreservesOverrideProvenance()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "product-override",
            universe,
            new[] { CreateBatch(universe.Version, [0], messageTimestampBase: 100) },
            partitionCount: 1,
            shardCount: 1,
            options: new RadarProcessingProductionPipelineOptions(workerCount: 2));
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);
        var detail = RadarPulseProductPipelineMapper.ToProductRunDetail(
            result,
            new RadarPulseProductInputSummary(
                RadarPulseProductInputKind.Synthetic,
                "override run",
                "synthetic",
                BatchCount: 1,
                EventCount: 1));

        var workerCount = Assert.Single(
            detail.Configuration.Values,
            static value => value.Name == nameof(RadarProcessingProductionPipelineResolvedConfiguration.WorkerCount));
        Assert.Equal("2", workerCount.Value);
        Assert.Equal(RadarPulseProductOptionSource.ExplicitOverride, workerCount.Source);
        var providerMode = Assert.Single(
            detail.Configuration.Values,
            static value => value.Name == nameof(RadarProcessingProductionPipelineResolvedConfiguration.ProviderMode));
        Assert.Equal(RadarPulseProductOptionSource.Profile, providerMode.Source);
    }

    [Fact]
    public async Task BlockedProductionPipelineResultMapsFirstBlockerAndFallback()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "product-blocked",
            universe,
            new[] { CreateBatch(universe.Version, [0], messageTimestampBase: 100) },
            partitionCount: 1,
            shardCount: 1,
            options: new RadarProcessingProductionPipelineOptions(workerCount: 0));
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);
        var detail = RadarPulseProductPipelineMapper.ToProductRunDetail(
            result,
            new RadarPulseProductInputSummary(
                RadarPulseProductInputKind.Synthetic,
                "blocked run",
                "synthetic",
                BatchCount: 1,
                EventCount: 1));

        Assert.False(detail.IsReady);
        Assert.False(detail.HasReadModel);
        Assert.Equal(RadarPulseProductRunState.Blocked, detail.Summary.State);
        Assert.Equal(
            RadarPulseProductFallbackRecommendation.FixConfiguration,
            detail.Summary.FallbackRecommendation);
        Assert.Contains(
            nameof(RadarProcessingProductionPipelineOptions.WorkerCount),
            detail.Summary.FirstBlockingReason,
            StringComparison.Ordinal);
        Assert.Null(detail.Diagnostics);
        Assert.Empty(detail.Batches);
        Assert.Empty(detail.Sources);
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
}
