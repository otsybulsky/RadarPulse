using System.Text.Json;
using RadarPulse.Application.Product;
using RadarPulse.Http;
using RadarPulse.Http.Product;
using RadarPulse.Infrastructure.Product;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RadarPulse.Tests.Product;

public sealed partial class RadarPulseProductHttpHostTests
{
    [Fact]
    public void RouteMapperExposesProductPipelineEndpoints()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRadarPulseProductPipelineApi>(
            RadarPulseProductPipelineApiContractTestFactory.Create(new RadarPulseProductPipelineService()));
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
        Assert.Contains("/product/pipeline/host/demo-readiness", patterns);
    }
}
