namespace RadarPulse.Http.Product;

/// <summary>
/// Configuration options for the local product HTTP host.
/// </summary>
/// <remarks>
/// These options support the accepted local same-origin demo/readiness package.
/// They are not production deployment, auth, TLS, or public CORS hardening
/// settings.
/// </remarks>
public sealed class RadarPulseProductHttpOptions
{
    /// <summary>
    /// Local product run history JSON path used when file-backed history is enabled.
    /// </summary>
    public string HistoryPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "radarpulse-product-history.json");

    /// <summary>
    /// Uses process-local history instead of deterministic local file history.
    /// </summary>
    public bool UseInMemoryHistory { get; set; }

    /// <summary>
    /// Enables the development CORS policy for the Angular operator UI dev server.
    /// </summary>
    public bool EnableOperatorUiCors { get; set; } = true;

    /// <summary>
    /// Allowed local operator UI origins for the development CORS policy.
    /// </summary>
    public string[] OperatorUiCorsOrigins { get; set; } =
    [
        "http://localhost:4200"
    ];

    /// <summary>
    /// Enables same-origin delivery of the built operator UI static assets.
    /// </summary>
    public bool EnableOperatorUiStaticFiles { get; set; } = true;

    /// <summary>
    /// Path to the built Angular browser assets containing index.html.
    /// </summary>
    public string OperatorUiStaticAssetPath { get; set; } =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "OperatorUi",
            "dist",
            "OperatorUi",
            "browser"));
}
