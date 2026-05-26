using System.Text.Json;
using RadarPulse.Application.Product;
using RadarPulse.Http.Product;
using RadarPulse.Infrastructure.Product;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductHttpHostTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void RouteMapperExposesProductPipelineEndpoints()
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

        Assert.Contains("/product/pipeline/runs/demo", patterns);
        Assert.Contains("/product/pipeline/runs/archive", patterns);
        Assert.Contains("/product/pipeline/runs", patterns);
        Assert.Contains("/product/pipeline/runs/latest", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/batches", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/batches/{providerSequence:long}", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/sources", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/sources/{sourceId:int}", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/handlers/{sourceId:int}/{fieldName}", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/diagnostics", patterns);
        Assert.Contains("/product/pipeline/runs/{runId}/capacity", patterns);
        Assert.Contains("/product/pipeline/host/readiness", patterns);
    }

    [Fact]
    public async Task DemoRouteMapsToProductServiceCreatedResponse()
    {
        var api = new RadarPulseProductPipelineApiContract();

        var result = await RadarPulseProductHttpEndpoints.RunDemoAsync(
            api,
            new RadarPulseProductPipelineSyntheticRunRequest(
                "http-demo",
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum),
            CancellationToken.None);
        var response = await ExecuteAsync<RadarPulseProductRunDetail>(result);

        Assert.True(response.IsSuccess);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("http-demo", response.Body!.RunId);
        Assert.Equal(RadarPulseProductHandlerMode.MergeableDelta, response.Body.Summary.HandlerMode);
    }

    [Fact]
    public async Task QueryRoutesMapSuccessAndNotFoundResponses()
    {
        var api = new RadarPulseProductPipelineApiContract();
        await api.RunDemoAsync(new RadarPulseProductPipelineSyntheticRunRequest("http-query"));

        var list = await ExecuteAsync<IReadOnlyList<RadarPulseProductRunSummary>>(
            RadarPulseProductHttpEndpoints.ListRuns(api));
        var latest = await ExecuteAsync<RadarPulseProductRunDetail>(
            RadarPulseProductHttpEndpoints.GetLatestRun(api));
        var detail = await ExecuteAsync<RadarPulseProductRunDetail>(
            RadarPulseProductHttpEndpoints.GetRun(api, "http-query"));
        var batches = await ExecuteAsync<IReadOnlyList<RadarPulseProductBatch>>(
            RadarPulseProductHttpEndpoints.ListBatches(api, "http-query"));
        var batch = await ExecuteAsync<RadarPulseProductBatch>(
            RadarPulseProductHttpEndpoints.GetBatch(api, "http-query", providerSequence: 1));
        var sources = await ExecuteAsync<IReadOnlyList<RadarPulseProductSource>>(
            RadarPulseProductHttpEndpoints.ListSources(api, "http-query"));
        var source = await ExecuteAsync<RadarPulseProductSource>(
            RadarPulseProductHttpEndpoints.GetSource(api, "http-query", sourceId: 0));
        var missing = await ExecuteAsync<RadarPulseProductRunDetail>(
            RadarPulseProductHttpEndpoints.GetRun(api, "missing"));

        Assert.True(list.IsSuccess);
        Assert.Single(list.Body!);
        Assert.Equal("http-query", latest.Body!.RunId);
        Assert.Equal("http-query", detail.Body!.RunId);
        Assert.NotEmpty(batches.Body!);
        Assert.Equal(1, batch.Body!.ProviderSequence);
        Assert.NotEmpty(sources.Body!);
        Assert.Equal(0, source.Body!.Identity.SourceId);
        Assert.False(missing.IsSuccess);
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task DiagnosticCapacityHandlerAndReadinessRoutesPreserveProductResponses()
    {
        var api = new RadarPulseProductPipelineApiContract();
        await api.RunDemoAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "http-readiness",
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum));

        var handler = await ExecuteAsync<RadarPulseProductHandlerOutput>(
            RadarPulseProductHttpEndpoints.GetHandlerOutput(
                api,
                "http-readiness",
                sourceId: 0,
                fieldName: "benchmark.events"));
        var diagnostics = await ExecuteAsync<RadarPulseProductDiagnostics>(
            RadarPulseProductHttpEndpoints.GetDiagnostics(api, "http-readiness"));
        var capacity = await ExecuteAsync<RadarPulseProductCapacityEvidence>(
            RadarPulseProductHttpEndpoints.GetCapacityEvidence(api, "http-readiness"));
        var readiness = await ExecuteAsync<RadarPulseProductRunHistoryReadiness>(
            RadarPulseProductHttpEndpoints.GetHistoryReadiness(api));

        Assert.True(handler.IsSuccess);
        Assert.Equal("benchmark.events", handler.Body!.Name);
        Assert.True(diagnostics.Body!.UsesOrderedHandlerDeltaMerge);
        Assert.True(capacity.Body!.IsReady);
        Assert.True(readiness.Body!.IsReady);
        Assert.Equal(1, readiness.Body.LoadedRunCount);
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
