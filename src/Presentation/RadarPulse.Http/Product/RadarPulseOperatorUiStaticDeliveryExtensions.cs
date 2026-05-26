using Microsoft.Extensions.FileProviders;

namespace RadarPulse.Http.Product;

public static class RadarPulseOperatorUiStaticDeliveryExtensions
{
    private const string ProductPipelinePathPrefix = "/product/pipeline";

    public static IApplicationBuilder UseRadarPulseOperatorUiStaticFiles(
        this IApplicationBuilder app,
        RadarPulseProductHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        if (!TryResolveOperatorUiStaticAssetRoot(options, out var assetRoot))
        {
            return app;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(assetRoot)
        });

        return app;
    }

    public static IEndpointRouteBuilder MapRadarPulseOperatorUiFallback(
        this IEndpointRouteBuilder endpoints,
        RadarPulseProductHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        endpoints.MapFallback(context => ServeOperatorUiFallbackAsync(context, options));
        return endpoints;
    }

    public static async Task ServeOperatorUiFallbackAsync(
        HttpContext context,
        RadarPulseProductHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (context.Request.Path.StartsWithSegments(ProductPipelinePathPrefix))
        {
            await Results.NotFound().ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        if (!TryResolveOperatorUiStaticAssetRoot(options, out var assetRoot))
        {
            await Results.NotFound().ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await Results.File(
                Path.Combine(assetRoot, "index.html"),
                contentType: "text/html")
            .ExecuteAsync(context)
            .ConfigureAwait(false);
    }

    public static bool TryResolveOperatorUiStaticAssetRoot(
        RadarPulseProductHttpOptions options,
        out string assetRoot)
    {
        ArgumentNullException.ThrowIfNull(options);

        assetRoot = string.Empty;
        if (!options.EnableOperatorUiStaticFiles ||
            string.IsNullOrWhiteSpace(options.OperatorUiStaticAssetPath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(options.OperatorUiStaticAssetPath);
            var indexPath = Path.Combine(fullPath, "index.html");
            if (!Directory.Exists(fullPath) || !File.Exists(indexPath))
            {
                return false;
            }

            assetRoot = fullPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
