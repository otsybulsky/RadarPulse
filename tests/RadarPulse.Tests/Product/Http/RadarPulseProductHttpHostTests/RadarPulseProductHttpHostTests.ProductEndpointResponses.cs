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
    public async Task DemoReadinessPreservesBlockedHistoryPosture()
    {
        using var staticRoot = OperatorUiStaticRoot.Create();
        using var blockedHistoryDirectory = TemporaryDirectory.Create();
        var service = RadarPulseProductPipelineService.CreateWithFileHistory(blockedHistoryDirectory.Path);
        var api = RadarPulseProductPipelineApiContractTestFactory.Create(service);

        var response = await ExecuteAsync<RadarPulseProductDemoReadiness>(
            RadarPulseProductHttpEndpoints.GetDemoReadiness(
                api,
                new RadarPulseProductHttpOptions
                {
                    HistoryPath = blockedHistoryDirectory.Path,
                    OperatorUiStaticAssetPath = staticRoot.Path
                }));

        Assert.True(response.IsSuccess);
        Assert.False(response.Body!.IsReady);
        Assert.False(response.Body.History.IsReady);
        Assert.True(response.Body.OperatorUi.IsReady);
        Assert.NotEmpty(response.Body.FirstBlockingReason);
        Assert.Equal(blockedHistoryDirectory.Path, response.Body.HistoryPath);
    }

    [Fact]
    public async Task ProductHttpServicesAllowLocalOperatorUiCorsByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddRadarPulseProductHttp(configuration);
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<ICorsPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(
            new DefaultHttpContext
            {
                RequestServices = provider
            },
            RadarPulseProductHttpServiceCollectionExtensions.OperatorUiCorsPolicyName);

        Assert.NotNull(policy);
        Assert.Contains("http://localhost:4200", policy!.Origins);
    }

    [Fact]
    public async Task DemoRouteMapsToProductServiceCreatedResponse()
    {
        var api = RadarPulseProductPipelineApiContractTestFactory.Create(new RadarPulseProductPipelineService());

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
        var api = RadarPulseProductPipelineApiContractTestFactory.Create(new RadarPulseProductPipelineService());
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
        var api = RadarPulseProductPipelineApiContractTestFactory.Create(new RadarPulseProductPipelineService());
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
}
