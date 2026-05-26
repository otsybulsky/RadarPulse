namespace RadarPulse.Http.Product;

public sealed class RadarPulseProductHttpOptions
{
    public string HistoryPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "radarpulse-product-history.json");

    public bool UseInMemoryHistory { get; set; }
}
