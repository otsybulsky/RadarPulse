using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProductionPipelineFallbackTests
{
    [Fact]
    public void StopAcceptingKeepsDurableStateVisible()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            var coordinator = new RadarProcessingProductionPipelineControlCoordinator();

            var result = coordinator.StopAccepting(CreateRequest("stop", universe, path));

            Assert.Equal(RadarProcessingProductionPipelineFallbackAction.StopAccepting, result.Action);
            Assert.Equal(RadarProcessingProductionPipelineRunState.Stopped, result.OperatorSummary.RunState);
            Assert.False(result.IsReady);
            Assert.Equal(1, result.AdapterSummary.QueueSummary.PendingEnvelopeCount);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Pending, result.OperatorSummary.FirstBlockingState);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DrainAcceptedCompletesPendingWorkInProviderSequence()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 2);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));
            var coordinator = new RadarProcessingProductionPipelineControlCoordinator();

            var result = await coordinator.DrainAcceptedAsync(CreateRequest("drain", universe, path));

            Assert.Equal(RadarProcessingProductionPipelineFallbackAction.DrainAccepted, result.Action);
            Assert.Equal(2, result.DrainedProcessingCount);
            Assert.True(result.IsReady);
            Assert.Equal(2, result.AdapterSummary.QueueSummary.ReleasedEnvelopeCount);
            Assert.False(result.AdapterSummary.QueueSummary.HasBlockingEnvelope);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CancelOpenMarksOpenWorkReleasedAndVisible()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 2);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));
            queue.ClaimNext("worker-a");
            var coordinator = new RadarProcessingProductionPipelineControlCoordinator();

            var result = coordinator.CancelOpenAndRelease(
                CreateRequest("cancel", universe, path),
                "operator canceled");

            Assert.Equal(RadarProcessingProductionPipelineFallbackAction.CancelOpenAndRelease, result.Action);
            Assert.Equal(2, result.CanceledOpenCount);
            Assert.Equal(2, result.ReleasedCanceledCount);
            Assert.Equal(2, result.AdapterSummary.QueueSummary.ReleasedEnvelopeCount);
            Assert.Equal(
                RadarProcessingProductionPipelineFallbackRecommendation.CleanupCanceledEnvelope,
                result.OperatorSummary.FallbackRecommendation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UnsafeBorrowedProviderFallbackIsRejected()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var coordinator = new RadarProcessingProductionPipelineControlCoordinator();

            var result = coordinator.RejectUnsafeFallback(
                CreateRequest("unsafe", universe, path),
                "borrowed fallback requested");

            Assert.Equal(RadarProcessingProductionPipelineFallbackAction.RejectUnsafeFallback, result.Action);
            Assert.False(result.IsReady);
            Assert.Equal(
                RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration,
                result.OperatorSummary.FallbackRecommendation);
        Assert.Contains(
            "fallback",
            result.OperatorSummary.FirstBlockingReason,
            StringComparison.OrdinalIgnoreCase);
            Assert.Contains("borrowed fallback requested", result.OperatorSummary.Warnings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotFallbackStillPublishesBffDiagnostics()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var request = new RadarProcessingProductionPipelineRunRequest(
            "snapshot-bff",
            universe,
            new[] { CreateBatch(universe.Version, [0], messageTimestampBase: 100) },
            partitionCount: 1,
            shardCount: 1,
            handlers: new IRadarSourceProcessingHandler[] { new CountingHandler() });
        var runner = new RadarProcessingProductionPipelineRunner();

        var result = await runner.RunAsync(request);

        Assert.True(result.IsCompleted);
        Assert.True(result.ReadModelStore.TryGetDiagnostics("snapshot-bff", out var diagnostics));
        Assert.True(diagnostics!.UsesSequentialHandlerFallback);
        Assert.Contains("sequential fallback", diagnostics.Warnings[0], StringComparison.Ordinal);
    }

    private static RadarProcessingProductionPipelineRecoveryRequest CreateRequest(
        string runId,
        RadarSourceUniverse universe,
        string path) =>
        new(
            runId,
            universe,
            path,
            partitionCount: universe.SourceCount,
            shardCount: Math.Min(2, universe.SourceCount));

    private static RadarProcessingDurableEnvelopeQueue CreateQueue(
        string path) =>
        new(new RadarProcessingFileDurableEnvelopeStore(path));

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

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
            "radarpulse-m027-fallback-",
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
}
