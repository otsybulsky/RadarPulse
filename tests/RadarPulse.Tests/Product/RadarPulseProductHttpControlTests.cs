using System.Text.Json;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Http.Product;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductHttpControlTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void RouteMapperExposesProductControlEndpoints()
    {
        var services = new ServiceCollection();
        services.AddSingleton<RadarPulseProductPipelineApiContract>();
        services.AddRouting();
        var provider = services.BuildServiceProvider();
        var endpoints = new RouteBuilderStub(provider);

        endpoints.MapRadarPulseProductPipeline();
        var patterns = endpoints.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("/product/pipeline/controls/stop-accepting", patterns);
        Assert.Contains("/product/pipeline/controls/drain-accepted", patterns);
        Assert.Contains("/product/pipeline/controls/cancel-open-release", patterns);
        Assert.Contains("/product/pipeline/controls/reject-unsafe-fallback", patterns);
    }

    [Fact]
    public async Task ControlRoutesReturnProductControlSummaries()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("durable.json");
        SeedDurableStore(path);
        var api = new RadarPulseProductPipelineApiContract();
        var request = new RadarPulseProductPipelineControlRequest(
            "http-control",
            RadarPulseProductControlAction.RejectUnsafeFallback,
            path,
            SourceCount: 1);

        var stop = await ExecuteAsync<RadarPulseProductControlSummary>(
            await RadarPulseProductHttpEndpoints.StopAcceptingAsync(
                api,
                request,
                CancellationToken.None));
        var cancel = await ExecuteAsync<RadarPulseProductControlSummary>(
            await RadarPulseProductHttpEndpoints.CancelOpenAndReleaseAsync(
                api,
                request,
                CancellationToken.None));

        Assert.True(stop.IsSuccess);
        Assert.Equal("StopAccepting", stop.Body!.Action);
        Assert.True(cancel.IsSuccess);
        Assert.Equal("CancelOpenAndRelease", cancel.Body!.Action);
    }

    [Fact]
    public async Task UnsafeFallbackRouteReturnsRejectedProductPosture()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("durable.json");
        SeedDurableStore(path);
        var api = new RadarPulseProductPipelineApiContract();

        var response = await ExecuteAsync<RadarPulseProductControlSummary>(
            await RadarPulseProductHttpEndpoints.RejectUnsafeFallbackAsync(
                api,
                new RadarPulseProductPipelineControlRequest(
                    "http-unsafe",
                    RadarPulseProductControlAction.StopAccepting,
                    path,
                    SourceCount: 1,
                    Message: "borrowed fallback requested"),
                CancellationToken.None));

        Assert.True(response.IsSuccess);
        Assert.False(response.Body!.OperatorSummary.IsReady);
        Assert.Equal("RejectUnsafeFallback", response.Body.Action);
        Assert.Contains(
            "borrowed fallback requested",
            response.Body.OperatorSummary.Warnings);
    }

    [Fact]
    public async Task BadRequestAndNotFoundRoutesReturnStableProductResponses()
    {
        var api = new RadarPulseProductPipelineApiContract();

        var badRequest = await ExecuteAsync<RadarPulseProductRunDetail>(
            await RadarPulseProductHttpEndpoints.RunDemoAsync(
                api,
                new RadarPulseProductPipelineSyntheticRunRequest(
                    "http-bad-request",
                    SourceCount: 0),
                CancellationToken.None));
        var missing = await ExecuteAsync<RadarPulseProductRunDetail>(
            RadarPulseProductHttpEndpoints.GetRun(api, "missing-run"));
        var missingControl = await ExecuteAsync<RadarPulseProductControlSummary>(
            await RadarPulseProductHttpEndpoints.DrainAcceptedAsync(
                api,
                new RadarPulseProductPipelineControlRequest(
                    "http-control-missing",
                    RadarPulseProductControlAction.StopAccepting,
                    DurableStorePath: string.Empty),
                CancellationToken.None));

        Assert.False(badRequest.IsSuccess);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.False(missing.IsSuccess);
        Assert.Equal(404, missing.StatusCode);
        Assert.False(missingControl.IsSuccess);
        Assert.Equal(404, missingControl.StatusCode);
    }

    [Fact]
    public async Task BlockedHistoryReadinessStaysVisibleThroughHttp()
    {
        using var directory = TemporaryDirectory.Create();
        var service = RadarPulseProductPipelineService.CreateWithFileHistory(directory.Path);
        var api = new RadarPulseProductPipelineApiContract(service);

        var response = await ExecuteAsync<RadarPulseProductRunHistoryReadiness>(
            RadarPulseProductHttpEndpoints.GetHistoryReadiness(api));

        Assert.True(response.IsSuccess);
        Assert.False(response.Body!.IsReady);
        Assert.Contains("must be a file path", response.Body.FirstBlockingReason);
    }

    private static async Task<RadarPulseProductApiResponse<T>> ExecuteAsync<T>(
        IResult result)
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        context.RequestServices = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);
        body.Position = 0;

        var response = await JsonSerializer.DeserializeAsync<RadarPulseProductApiResponse<T>>(
            body,
            JsonOptions);
        Assert.NotNull(response);
        Assert.Equal(context.Response.StatusCode, response!.StatusCode);
        return response;
    }

    private static void SeedDurableStore(
        string path)
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 1,
            rangeBandCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue(
            new RadarProcessingFileDurableEnvelopeStore(path));
        queue.Accept(
            new RadarProcessingDurableBatchId("batch-0"),
            CreateBatch(universe.Version));
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: 1,
            initialPayloadCapacity: 1);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: sourceUniverseVersion),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: new byte[] { 1 });
        return builder.Build();
    }

    private sealed class TemporaryDirectory :
        IDisposable
    {
        private TemporaryDirectory(
            string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "radarpulse-m029-http-control-",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string File(
            string fileName) =>
            System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class RouteBuilderStub :
        IEndpointRouteBuilder
    {
        public RouteBuilderStub(
            IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            DataSources = new List<EndpointDataSource>();
        }

        public IServiceProvider ServiceProvider { get; }

        public ICollection<EndpointDataSource> DataSources { get; }

        public IApplicationBuilder CreateApplicationBuilder() =>
            throw new NotSupportedException();
    }
}
