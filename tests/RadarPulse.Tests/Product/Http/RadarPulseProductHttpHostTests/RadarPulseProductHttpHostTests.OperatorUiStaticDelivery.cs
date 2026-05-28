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
    public void OperatorUiStaticAssetRootRequiresEnabledExistingIndex()
    {
        using var staticRoot = OperatorUiStaticRoot.Create();

        Assert.True(RadarPulseOperatorUiStaticDeliveryExtensions.TryResolveOperatorUiStaticAssetRoot(
            new RadarPulseProductHttpOptions
            {
                OperatorUiStaticAssetPath = staticRoot.Path
            },
            out var resolved));
        Assert.Equal(System.IO.Path.GetFullPath(staticRoot.Path), resolved);

        Assert.False(RadarPulseOperatorUiStaticDeliveryExtensions.TryResolveOperatorUiStaticAssetRoot(
            new RadarPulseProductHttpOptions
            {
                EnableOperatorUiStaticFiles = false,
                OperatorUiStaticAssetPath = staticRoot.Path
            },
            out _));
        Assert.False(RadarPulseOperatorUiStaticDeliveryExtensions.TryResolveOperatorUiStaticAssetRoot(
            new RadarPulseProductHttpOptions
            {
                OperatorUiStaticAssetPath = System.IO.Path.Combine(staticRoot.Path, "missing")
            },
            out _));
    }

    [Fact]
    public async Task OperatorUiFallbackServesIndexForLocalUiRoutes()
    {
        using var staticRoot = OperatorUiStaticRoot.Create();
        var context = CreateHttpContext("/runs/smoke-run");

        await RadarPulseOperatorUiStaticDeliveryExtensions.ServeOperatorUiFallbackAsync(
            context,
            new RadarPulseProductHttpOptions
            {
                OperatorUiStaticAssetPath = staticRoot.Path
            });

        var body = await ReadBodyAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.StartsWith("text/html", context.Response.ContentType, StringComparison.Ordinal);
        Assert.Contains("operator-ui-shell", body);
    }

    [Fact]
    public async Task OperatorUiFallbackDoesNotInterceptProductApiRoutes()
    {
        using var staticRoot = OperatorUiStaticRoot.Create();
        var context = CreateHttpContext("/product/pipeline/runs/missing");

        await RadarPulseOperatorUiStaticDeliveryExtensions.ServeOperatorUiFallbackAsync(
            context,
            new RadarPulseProductHttpOptions
            {
                OperatorUiStaticAssetPath = staticRoot.Path
            });

        Assert.Equal(404, context.Response.StatusCode);
    }

    [Fact]
    public async Task OperatorUiFallbackReturnsNotFoundWhenStaticDeliveryIsUnavailable()
    {
        var context = CreateHttpContext("/runs/smoke-run");

        await RadarPulseOperatorUiStaticDeliveryExtensions.ServeOperatorUiFallbackAsync(
            context,
            new RadarPulseProductHttpOptions
            {
                EnableOperatorUiStaticFiles = false
            });

        Assert.Equal(404, context.Response.StatusCode);
    }
}
