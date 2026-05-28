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
    public async Task DemoReadinessReportsReadyHistoryAndStaticUi()
    {
        using var staticRoot = OperatorUiStaticRoot.Create();
        var api = RadarPulseProductPipelineApiContractTestFactory.Create(new RadarPulseProductPipelineService());
        await api.RunDemoAsync(new RadarPulseProductPipelineSyntheticRunRequest("demo-readiness"));

        var response = await ExecuteAsync<RadarPulseProductDemoReadiness>(
            RadarPulseProductHttpEndpoints.GetDemoReadiness(
                api,
                new RadarPulseProductHttpOptions
                {
                    OperatorUiStaticAssetPath = staticRoot.Path
                }));

        Assert.True(response.IsSuccess);
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Body!.IsReady);
        Assert.Equal(string.Empty, response.Body.FirstBlockingReason);
        Assert.True(response.Body.ProductApi.IsReady);
        Assert.True(response.Body.History.IsReady);
        Assert.True(response.Body.OperatorUi.IsReady);
        Assert.Equal(System.IO.Path.GetFullPath(staticRoot.Path), response.Body.OperatorUiStaticAssetRoot);
        Assert.Contains(
            "public production deployment",
            response.Body.NonClaims);
    }

    [Fact]
    public async Task DemoReadinessBlocksMissingStaticUi()
    {
        var api = RadarPulseProductPipelineApiContractTestFactory.Create(new RadarPulseProductPipelineService());
        var missingPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"radarpulse-missing-ui-{Guid.NewGuid():N}");

        var response = await ExecuteAsync<RadarPulseProductDemoReadiness>(
            RadarPulseProductHttpEndpoints.GetDemoReadiness(
                api,
                new RadarPulseProductHttpOptions
                {
                    OperatorUiStaticAssetPath = missingPath
                }));

        Assert.True(response.IsSuccess);
        Assert.False(response.Body!.IsReady);
        Assert.True(response.Body.History.IsReady);
        Assert.False(response.Body.OperatorUi.IsReady);
        Assert.Contains("Operator UI static asset root is unavailable", response.Body.FirstBlockingReason);
    }
}
