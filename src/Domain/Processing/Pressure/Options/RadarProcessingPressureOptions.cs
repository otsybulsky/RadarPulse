namespace RadarPulse.Domain.Processing;

/// <summary>
/// Scoring weights and band thresholds for a single pressure sample.
/// </summary>
public sealed record RadarProcessingPressureOptions
{
    /// <summary>
    /// Default pressure scoring configuration.
    /// </summary>
    public static RadarProcessingPressureOptions Default { get; } = new();

    /// <summary>
    /// Creates pressure scoring options with ordered non-negative thresholds.
    /// </summary>
    public RadarProcessingPressureOptions(
        double eventWeight = 1.0,
        double payloadValueWeight = 0.001,
        double rawValueChecksumWeight = 0.0,
        double coldThreshold = 0.0,
        double warmThreshold = 10_000.0,
        double hotThreshold = 50_000.0,
        double superHotThreshold = 100_000.0)
    {
        ThrowIfNegative(eventWeight, nameof(eventWeight));
        ThrowIfNegative(payloadValueWeight, nameof(payloadValueWeight));
        ThrowIfNegative(rawValueChecksumWeight, nameof(rawValueChecksumWeight));
        ThrowIfNegative(coldThreshold, nameof(coldThreshold));
        ThrowIfNegative(warmThreshold, nameof(warmThreshold));
        ThrowIfNegative(hotThreshold, nameof(hotThreshold));
        ThrowIfNegative(superHotThreshold, nameof(superHotThreshold));

        if (warmThreshold < coldThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warmThreshold),
                warmThreshold,
                "Warm threshold must be greater than or equal to cold threshold.");
        }

        if (hotThreshold < warmThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hotThreshold),
                hotThreshold,
                "Hot threshold must be greater than or equal to warm threshold.");
        }

        if (superHotThreshold < hotThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(superHotThreshold),
                superHotThreshold,
                "Super-hot threshold must be greater than or equal to hot threshold.");
        }

        EventWeight = eventWeight;
        PayloadValueWeight = payloadValueWeight;
        RawValueChecksumWeight = rawValueChecksumWeight;
        ColdThreshold = coldThreshold;
        WarmThreshold = warmThreshold;
        HotThreshold = hotThreshold;
        SuperHotThreshold = superHotThreshold;
    }

    /// <summary>
    /// Weight applied to routed event count.
    /// </summary>
    public double EventWeight { get; }

    /// <summary>
    /// Weight applied to routed payload value count.
    /// </summary>
    public double PayloadValueWeight { get; }

    /// <summary>
    /// Weight applied to raw payload checksum contribution.
    /// </summary>
    public double RawValueChecksumWeight { get; }

    /// <summary>
    /// Score at or below which pressure is cold.
    /// </summary>
    public double ColdThreshold { get; }

    /// <summary>
    /// Score at or above which pressure is warm.
    /// </summary>
    public double WarmThreshold { get; }

    /// <summary>
    /// Score at or above which pressure is hot.
    /// </summary>
    public double HotThreshold { get; }

    /// <summary>
    /// Score at or above which pressure is super-hot.
    /// </summary>
    public double SuperHotThreshold { get; }

    /// <summary>
    /// Calculates a pressure score from routed event and payload metrics.
    /// </summary>
    public RadarProcessingPressureScore Score(RadarProcessingRouteMetrics metrics) =>
        new(
            checked(
                (metrics.EventCount * EventWeight) +
                (metrics.PayloadValueCount * PayloadValueWeight) +
                (metrics.RawValueChecksum * RawValueChecksumWeight)));

    /// <summary>
    /// Classifies a score using this sample's static thresholds.
    /// </summary>
    public RadarProcessingPressureBand Classify(RadarProcessingPressureScore score)
    {
        if (score.Value >= SuperHotThreshold)
        {
            return RadarProcessingPressureBand.SuperHot;
        }

        if (score.Value >= HotThreshold)
        {
            return RadarProcessingPressureBand.Hot;
        }

        if (score.Value >= WarmThreshold)
        {
            return RadarProcessingPressureBand.Warm;
        }

        return score.Value <= ColdThreshold
            ? RadarProcessingPressureBand.Cold
            : RadarProcessingPressureBand.Normal;
    }

    private static void ThrowIfNegative(
        double value,
        string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and non-negative.");
        }
    }
}
