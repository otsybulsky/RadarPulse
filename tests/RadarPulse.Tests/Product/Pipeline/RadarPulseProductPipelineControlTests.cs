using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductPipelineControlTests
{
    [Fact]
    public async Task StopAcceptingControlReportsPreservedDurableState()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            var service = new RadarPulseProductPipelineService();

            var result = await service.ApplyControlAsync(
                new RadarPulseProductPipelineControlRequest(
                    "product-stop",
                    RadarPulseProductControlAction.StopAccepting,
                    path,
                    SourceCount: 1));

            Assert.True(result.Found);
            Assert.Equal("StopAccepting", result.Value!.Action);
            Assert.False(result.Value.OperatorSummary.IsReady);
            Assert.Equal(RadarPulseProductRunState.Stopped, result.Value.OperatorSummary.RunState);
            Assert.Equal("Pending", result.Value.OperatorSummary.FirstBlockingState);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DrainAcceptedControlReportsDrainedProcessingCount()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 2);
            var queue = CreateQueue(path);
            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));
            var service = new RadarPulseProductPipelineService();

            var result = await service.ApplyControlAsync(
                new RadarPulseProductPipelineControlRequest(
                    "product-drain",
                    RadarPulseProductControlAction.DrainAccepted,
                    path,
                    SourceCount: 2));

            Assert.True(result.Found);
            Assert.Equal("DrainAccepted", result.Value!.Action);
            Assert.Equal(2, result.Value.DrainedProcessingCount);
            Assert.True(result.Value.OperatorSummary.IsReady);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancelOpenAndReleaseControlReportsCanceledAndReleasedCounts()
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
            var service = new RadarPulseProductPipelineService();

            var result = await service.ApplyControlAsync(
                new RadarPulseProductPipelineControlRequest(
                    "product-cancel",
                    RadarPulseProductControlAction.CancelOpenAndRelease,
                    path,
                    SourceCount: 2,
                    Message: "operator canceled"));

            Assert.True(result.Found);
            Assert.Equal("CancelOpenAndRelease", result.Value!.Action);
            Assert.Equal(2, result.Value.CanceledOpenCount);
            Assert.Equal(2, result.Value.ReleasedCanceledCount);
            Assert.Equal(
                RadarPulseProductFallbackRecommendation.CleanupCanceledEnvelope,
                result.Value.OperatorSummary.FallbackRecommendation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UnsafeFallbackControlIsRejectedWithProductReason()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var service = new RadarPulseProductPipelineService();

            var result = await service.ApplyControlAsync(
                new RadarPulseProductPipelineControlRequest(
                    "product-unsafe",
                    RadarPulseProductControlAction.RejectUnsafeFallback,
                    path,
                    SourceCount: 1,
                    Message: "borrowed fallback requested"));

            Assert.True(result.Found);
            Assert.Equal("RejectUnsafeFallback", result.Value!.Action);
            Assert.False(result.Value.OperatorSummary.IsReady);
            Assert.Equal(
                RadarPulseProductFallbackRecommendation.FixConfiguration,
                result.Value.OperatorSummary.FallbackRecommendation);
            Assert.Contains(
                "fallback",
                result.Value.OperatorSummary.FirstBlockingReason,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("borrowed fallback requested", result.Value.OperatorSummary.Warnings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ControlWithoutDurableStorePathFailsClosed()
    {
        var service = new RadarPulseProductPipelineService();

        var result = await service.ApplyControlAsync(
            new RadarPulseProductPipelineControlRequest(
                "product-missing-control-store",
                RadarPulseProductControlAction.StopAccepting,
                string.Empty));

        Assert.False(result.Found);
        Assert.Contains("durable store path", result.Message);
    }

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
            "radarpulse-m028-control-",
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
