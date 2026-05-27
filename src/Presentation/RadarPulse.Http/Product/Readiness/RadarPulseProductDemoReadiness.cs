using RadarPulse.Application.Product;

namespace RadarPulse.Http.Product;

public sealed record RadarPulseProductDemoReadiness(
    bool IsReady,
    string FirstBlockingReason,
    RadarPulseProductDemoReadinessItem ProductApi,
    RadarPulseProductDemoReadinessItem History,
    RadarPulseProductDemoReadinessItem OperatorUi,
    string HistoryPath,
    string OperatorUiStaticAssetPath,
    string OperatorUiStaticAssetRoot,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NonClaims)
{
    public static RadarPulseProductDemoReadiness From(
        RadarPulseProductRunHistoryReadiness historyReadiness,
        RadarPulseProductHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(historyReadiness);
        ArgumentNullException.ThrowIfNull(options);

        var productApi = new RadarPulseProductDemoReadinessItem(
            "product-api",
            true,
            "ready",
            "Product pipeline HTTP routes are mapped under /product/pipeline.",
            string.Empty);

        var history = new RadarPulseProductDemoReadinessItem(
            "history",
            historyReadiness.IsReady,
            historyReadiness.IsReady ? "ready" : "blocked",
            historyReadiness.StorageIdentity,
            historyReadiness.FirstBlockingReason);

        var operatorUi = CreateOperatorUiItem(options, out var operatorUiStaticAssetRoot);
        var firstBlockingReason = ResolveFirstBlockingReason(history, operatorUi);
        var warnings = historyReadiness.Warnings
            .Concat(PackageWarnings)
            .ToArray();

        return new RadarPulseProductDemoReadiness(
            history.IsReady && operatorUi.IsReady,
            firstBlockingReason,
            productApi,
            history,
            operatorUi,
            options.HistoryPath,
            options.OperatorUiStaticAssetPath,
            operatorUiStaticAssetRoot,
            warnings,
            PackageNonClaims);
    }

    private static RadarPulseProductDemoReadinessItem CreateOperatorUiItem(
        RadarPulseProductHttpOptions options,
        out string operatorUiStaticAssetRoot)
    {
        operatorUiStaticAssetRoot = string.Empty;
        if (!options.EnableOperatorUiStaticFiles)
        {
            return new RadarPulseProductDemoReadinessItem(
                "operator-ui",
                false,
                "blocked",
                "Operator UI static file delivery is disabled.",
                "Operator UI static file delivery is disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.OperatorUiStaticAssetPath))
        {
            return new RadarPulseProductDemoReadinessItem(
                "operator-ui",
                false,
                "blocked",
                "Operator UI static asset path is not configured.",
                "Operator UI static asset path is not configured.");
        }

        if (!RadarPulseOperatorUiStaticDeliveryExtensions.TryResolveOperatorUiStaticAssetRoot(
                options,
                out operatorUiStaticAssetRoot))
        {
            return new RadarPulseProductDemoReadinessItem(
                "operator-ui",
                false,
                "blocked",
                "Operator UI static asset root is unavailable or missing index.html.",
                "Operator UI static asset root is unavailable or missing index.html.");
        }

        return new RadarPulseProductDemoReadinessItem(
            "operator-ui",
            true,
            "ready",
            operatorUiStaticAssetRoot,
            string.Empty);
    }

    private static string ResolveFirstBlockingReason(
        params RadarPulseProductDemoReadinessItem[] items) =>
        items.FirstOrDefault(item => !item.IsReady)?.FirstBlockingReason ?? string.Empty;

    private static readonly string[] PackageWarnings =
    [
        "Local demo/readiness packaging covers deterministic demo/archive-shaped workflows only.",
        "The same-origin host is a local RadarPulse.Http delivery path, not public production hosting."
    ];

    private static readonly string[] PackageNonClaims =
    [
        "true live radar network ingestion",
        "external broker/cloud queue/database adapter certification",
        "public production deployment",
        "authentication or authorization",
        "TLS termination",
        "production CORS hardening",
        "deployment automation or autoscaling",
        "cross-machine throughput certification",
        "exactly-once end-to-end production delivery"
    ];
}

public sealed record RadarPulseProductDemoReadinessItem(
    string Name,
    bool IsReady,
    string Status,
    string Detail,
    string FirstBlockingReason);
