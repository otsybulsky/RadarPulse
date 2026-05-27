using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProductionPipelineRecoveryTests
{
    [Fact]
    public async Task PipelineRecoveryCommitsCompletedEnvelopeAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            var claim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
            await session.ProcessClaimedAsync(claim);

            var recovery = new RadarProcessingProductionPipelineRecoveryRunner();
            var result = await recovery.RecoverAsync(
                new RadarProcessingProductionPipelineRecoveryRequest(
                    "recover-completed",
                    universe,
                    path,
                    partitionCount: 1,
                    shardCount: 1));

            Assert.True(result.IsReady);
            Assert.Equal(1, result.RecoveredCompletedCount);
            Assert.Equal([0L], result.CommittedResults.Select(static item => item.Sequence.Value).ToArray());
            Assert.Equal(RadarProcessingFileDurableEnvelopeStore.Kind, result.AdapterSummary.AdapterKind);
            Assert.Equal(1, result.AdapterSummary.QueueSummary.ReleasedEnvelopeCount);
            Assert.False(result.OperatorSummary.HasBlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ClaimedEnvelopeBlocksRestartedPipelineUntilRecoveryAction()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            queue.ClaimNext("worker-a");

            var recovery = new RadarProcessingProductionPipelineRecoveryRunner();
            var result = await recovery.RecoverAsync(
                new RadarProcessingProductionPipelineRecoveryRequest(
                    "recover-claimed",
                    universe,
                    path,
                    partitionCount: 1,
                    shardCount: 1));

            Assert.False(result.IsReady);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Claimed, result.OperatorSummary.FirstBlockingState);
            Assert.Equal(
                RadarProcessingProductionPipelineFallbackRecommendation.RecoverClaimedEnvelope,
                result.OperatorSummary.FallbackRecommendation);
            Assert.Contains("claimed envelope", result.OperatorSummary.FirstBlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FailedEnvelopeBlocksRecoveryWithRetryOrPoisonRecommendation()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            var claim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
            queue.Fail(claim.BatchId, "worker failed");

            var recovery = new RadarProcessingProductionPipelineRecoveryRunner();
            var result = await recovery.RecoverAsync(
                new RadarProcessingProductionPipelineRecoveryRequest(
                    "recover-failed",
                    universe,
                    path,
                    partitionCount: 1,
                    shardCount: 1));

            Assert.False(result.IsReady);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, result.OperatorSummary.FirstBlockingState);
            Assert.Equal(
                RadarProcessingProductionPipelineFallbackRecommendation.RetryOrPoisonEnvelope,
                result.OperatorSummary.FallbackRecommendation);
            Assert.Equal("worker failed", result.OperatorSummary.FirstBlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IncompatibleDurableStoreFailsClosed()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            File.WriteAllText(path, "{not-json");
            var universe = CreateUniverse(sourceCount: 1);

            var recovery = new RadarProcessingProductionPipelineRecoveryRunner();
            var result = await recovery.RecoverAsync(
                new RadarProcessingProductionPipelineRecoveryRequest(
                    "recover-incompatible",
                    universe,
                    path,
                    partitionCount: 1,
                    shardCount: 1));

            Assert.False(result.IsReady);
            Assert.Equal(
                RadarProcessingDurableAdapterCompatibilityStatus.Failed,
                result.AdapterSummary.CompatibilityStatus);
            Assert.Equal(
                RadarProcessingProductionPipelineFallbackRecommendation.InspectDurableAdapter,
                result.OperatorSummary.FallbackRecommendation);
            Assert.True(result.OperatorSummary.HasBlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RadarProcessingDurableEnvelopeQueue CreateQueue(
        string path) =>
        new(new RadarProcessingFileDurableEnvelopeStore(path));

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount),
                asyncExecution: RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution()));

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "radarpulse-m027-recovery-",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

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
