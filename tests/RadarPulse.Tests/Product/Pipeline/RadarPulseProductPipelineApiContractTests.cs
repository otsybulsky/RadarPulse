using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductPipelineApiContractTests
{
    [Fact]
    public async Task ApiRunCommandMapsToProductServiceResult()
    {
        var api = new RadarPulseProductPipelineApiContract(new RadarPulseProductPipelineService());

        var response = await api.RunDemoAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "api-run",
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum));

        Assert.True(response.IsSuccess);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("api-run", response.Body!.RunId);
        Assert.True(response.Body.IsReady);
        Assert.Equal(RadarPulseProductHandlerMode.MergeableDelta, response.Body.Summary.HandlerMode);
    }

    [Fact]
    public async Task ApiContractMapsListLatestAndDetailQueries()
    {
        var api = new RadarPulseProductPipelineApiContract(new RadarPulseProductPipelineService());
        await api.RunDemoAsync(new RadarPulseProductPipelineSyntheticRunRequest("api-first"));
        await api.RunDemoAsync(new RadarPulseProductPipelineSyntheticRunRequest("api-second"));

        var list = api.ListRuns();
        var latest = api.GetLatestRun();
        var detail = api.GetRun("api-first");
        var batches = api.ListBatches("api-second");
        var sources = api.ListSources("api-second");
        var diagnostics = api.GetDiagnostics("api-second");
        var capacity = api.GetCapacityEvidence("api-second");

        Assert.Equal(200, list.StatusCode);
        Assert.Equal(2, list.Body!.Count);
        Assert.Equal("api-second", latest.Body!.RunId);
        Assert.Equal("api-first", detail.Body!.RunId);
        Assert.NotEmpty(batches.Body!);
        Assert.NotEmpty(sources.Body!);
        Assert.True(diagnostics.Body!.ProcessingCompletenessPassed);
        Assert.True(capacity.Body!.IsReady);
    }

    [Fact]
    public async Task ApiContractMapsNotFoundAndBadRequestResponses()
    {
        var api = new RadarPulseProductPipelineApiContract(new RadarPulseProductPipelineService());

        var missing = api.GetRun("missing");
        var badRequest = await api.RunDemoAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "api-bad",
                SourceCount: 0));

        Assert.False(missing.IsSuccess);
        Assert.Equal(404, missing.StatusCode);
        Assert.Contains("not found", missing.Message);
        Assert.False(badRequest.IsSuccess);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Contains("SourceCount", badRequest.Message);
    }

    [Fact]
    public async Task ApiContractMapsControlResultAndUnsafeFallbackRejection()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = new RadarProcessingDurableEnvelopeQueue(
                new RadarProcessingFileDurableEnvelopeStore(path));
            queue.Accept(
                new RadarProcessingDurableBatchId("batch-0"),
                CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            var api = new RadarPulseProductPipelineApiContract(new RadarPulseProductPipelineService());

            var stop = await api.ApplyControlAsync(
                new RadarPulseProductPipelineControlRequest(
                    "api-stop",
                    RadarPulseProductControlAction.StopAccepting,
                    path,
                    SourceCount: 1));
            var unsafeFallback = await api.ApplyControlAsync(
                new RadarPulseProductPipelineControlRequest(
                    "api-unsafe",
                    RadarPulseProductControlAction.RejectUnsafeFallback,
                    path,
                    SourceCount: 1,
                    Message: "borrowed fallback requested"));

            Assert.True(stop.IsSuccess);
            Assert.Equal(200, stop.StatusCode);
            Assert.Equal("StopAccepting", stop.Body!.Action);
            Assert.False(unsafeFallback.Body!.OperatorSummary.IsReady);
            Assert.Equal("RejectUnsafeFallback", unsafeFallback.Body.Action);
            Assert.Contains("borrowed fallback requested", unsafeFallback.Body.OperatorSummary.Warnings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

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
            "radarpulse-m028-api-",
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
