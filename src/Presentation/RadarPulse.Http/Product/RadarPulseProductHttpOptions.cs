namespace RadarPulse.Http.Product;

public sealed class RadarPulseProductHttpOptions
{
    public string HistoryPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "radarpulse-product-history.json");

    public bool UseInMemoryHistory { get; set; }

    public bool EnableOperatorUiCors { get; set; } = true;

    public string[] OperatorUiCorsOrigins { get; set; } =
    [
        "http://localhost:4200"
    ];

    public bool EnableOperatorUiStaticFiles { get; set; } = true;

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
