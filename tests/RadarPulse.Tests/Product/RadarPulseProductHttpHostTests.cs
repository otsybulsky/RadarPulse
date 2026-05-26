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

    private static DefaultHttpContext CreateHttpContext(
        string path)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(
        DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class OperatorUiStaticRoot :
        IDisposable
    {
        private OperatorUiStaticRoot(
            string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static OperatorUiStaticRoot Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"radarpulse-operator-ui-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            File.WriteAllText(
                System.IO.Path.Combine(path, "index.html"),
                "<!doctype html><html><body>operator-ui-shell</body></html>");
            File.WriteAllText(
                System.IO.Path.Combine(path, "main.js"),
                "console.log('operator ui');");

            return new OperatorUiStaticRoot(path);
        }

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
