using RadarPulse.Domain.Streaming;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProductionPipelineGateTests
{
    [Fact]
    public async Task CapacityEvidenceCapturesSuccessfulPipelineContour()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "gate-success",
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
        var evidence = RadarProcessingProductionPipelineCapacityEvidence.FromRunResult(result);

        Assert.True(evidence.IsReady);
        Assert.True(evidence.ProcessingCompletenessPassed);
        Assert.Equal(2, evidence.AcceptedBatchCount);
        Assert.Equal(2, evidence.ProcessedBatchCount);
        Assert.Equal(2, evidence.CommittedBatchCount);
        Assert.Equal(RadarProcessingProductionPipelineHandlerMode.MergeableDelta, evidence.HandlerMode);
        Assert.Equal(RadarProcessingProductionPipelineDurableAdapterKind.File, evidence.DurableAdapterKind);
        Assert.Equal(0, evidence.TerminalRetainedBatchCount);
        Assert.Equal(0, evidence.TerminalRetainedPayloadBytes);
        Assert.Contains("provider=QueuedOwned", evidence.ConfigurationContour);
        Assert.Contains("activeBatches=4", evidence.ConfigurationContour);
    }

    [Fact]
    public async Task CapacityEvidencePreservesBlockedReason()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "gate-blocked",
            universe,
            new[] { CreateBatch(universe.Version, [0], messageTimestampBase: 100) },
            partitionCount: 1,
            shardCount: 1,
            options: new RadarProcessingProductionPipelineOptions(workerCount: 0));
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);
        var evidence = RadarProcessingProductionPipelineCapacityEvidence.FromRunResult(result);

        Assert.False(evidence.IsReady);
        Assert.False(evidence.ProcessingCompletenessPassed);
        Assert.Equal(0, evidence.AcceptedBatchCount);
        Assert.True(evidence.HasBlockingReason);
        Assert.Contains(nameof(RadarProcessingProductionPipelineOptions.WorkerCount), evidence.FirstBlockingReason);
    }

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
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
