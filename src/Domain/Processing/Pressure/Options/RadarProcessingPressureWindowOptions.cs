namespace RadarPulse.Domain.Processing;

/// <summary>
/// Rolling pressure window size and hysteresis thresholds.
/// </summary>
/// <remarks>
/// Enter thresholds promote a shard or partition into a hotter band. Exit
/// thresholds let it remain in that band until pressure drops far enough, which
/// prevents rapid band oscillation across adjacent samples.
/// </remarks>
public sealed record RadarProcessingPressureWindowOptions
{
    /// <summary>
    /// Default rolling pressure window configuration.
    /// </summary>
    public static RadarProcessingPressureWindowOptions Default { get; } = new();

    /// <summary>
    /// Creates rolling pressure window options.
    /// </summary>
    public RadarProcessingPressureWindowOptions(
        int sampleCapacity = 8,
        int minimumSampleCount = 3,
        double coldThreshold = 0.0,
        double warmExitThreshold = 8_000.0,
        double warmEnterThreshold = 10_000.0,
        double hotExitThreshold = 40_000.0,
        double hotEnterThreshold = 50_000.0,
        double superHotExitThreshold = 80_000.0,
        double superHotEnterThreshold = 100_000.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumSampleCount);
        ThrowIfInvalidThreshold(coldThreshold, nameof(coldThreshold));
        ThrowIfInvalidThreshold(warmExitThreshold, nameof(warmExitThreshold));
        ThrowIfInvalidThreshold(warmEnterThreshold, nameof(warmEnterThreshold));
        ThrowIfInvalidThreshold(hotExitThreshold, nameof(hotExitThreshold));
        ThrowIfInvalidThreshold(hotEnterThreshold, nameof(hotEnterThreshold));
        ThrowIfInvalidThreshold(superHotExitThreshold, nameof(superHotExitThreshold));
        ThrowIfInvalidThreshold(superHotEnterThreshold, nameof(superHotEnterThreshold));

        if (minimumSampleCount > sampleCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSampleCount),
                minimumSampleCount,
                "Minimum sample count must be less than or equal to sample capacity.");
        }

        EnsureThresholdOrder(
            coldThreshold,
            warmExitThreshold,
            warmEnterThreshold,
            hotExitThreshold,
            hotEnterThreshold,
            superHotExitThreshold,
            superHotEnterThreshold);

        SampleCapacity = sampleCapacity;
        MinimumSampleCount = minimumSampleCount;
        ColdThreshold = coldThreshold;
        WarmExitThreshold = warmExitThreshold;
        WarmEnterThreshold = warmEnterThreshold;
        HotExitThreshold = hotExitThreshold;
        HotEnterThreshold = hotEnterThreshold;
        SuperHotExitThreshold = superHotExitThreshold;
        SuperHotEnterThreshold = superHotEnterThreshold;
    }

    /// <summary>
    /// Maximum pressure samples retained by the rolling window.
    /// </summary>
    public int SampleCapacity { get; }

    /// <summary>
    /// Minimum samples required before rebalance planning is eligible.
    /// </summary>
    public int MinimumSampleCount { get; }

    /// <summary>
    /// Score at or below which pressure is cold.
    /// </summary>
    public double ColdThreshold { get; }

    /// <summary>
    /// Score below which a warm band can cool down.
    /// </summary>
    public double WarmExitThreshold { get; }

    /// <summary>
    /// Score at or above which a normal or cold band enters warm.
    /// </summary>
    public double WarmEnterThreshold { get; }

    /// <summary>
    /// Score below which a hot band can cool down to warm.
    /// </summary>
    public double HotExitThreshold { get; }

    /// <summary>
    /// Score at or above which pressure enters hot.
    /// </summary>
    public double HotEnterThreshold { get; }

    /// <summary>
    /// Score below which a super-hot band can cool down to hot.
    /// </summary>
    public double SuperHotExitThreshold { get; }

    /// <summary>
    /// Score at or above which pressure enters super-hot.
    /// </summary>
    public double SuperHotEnterThreshold { get; }

    /// <summary>
    /// Classifies a score using hysteresis against the previous pressure band.
    /// </summary>
    public RadarProcessingPressureBand Classify(
        RadarProcessingPressureScore score,
        RadarProcessingPressureBand previousBand)
    {
        if (score.Value >= SuperHotEnterThreshold ||
            (previousBand == RadarProcessingPressureBand.SuperHot &&
             score.Value >= SuperHotExitThreshold))
        {
            return RadarProcessingPressureBand.SuperHot;
        }

        if (score.Value >= HotEnterThreshold ||
            (previousBand is RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot &&
             score.Value >= HotExitThreshold))
        {
            return RadarProcessingPressureBand.Hot;
        }

        if (score.Value >= WarmEnterThreshold ||
            (previousBand is RadarProcessingPressureBand.Warm or RadarProcessingPressureBand.Hot
                or RadarProcessingPressureBand.SuperHot &&
             score.Value >= WarmExitThreshold))
        {
            return RadarProcessingPressureBand.Warm;
        }

        return score.Value <= ColdThreshold
            ? RadarProcessingPressureBand.Cold
            : RadarProcessingPressureBand.Normal;
    }

    private static void ThrowIfInvalidThreshold(
        double value,
        string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Threshold must be finite and non-negative.");
        }
    }

    private static void EnsureThresholdOrder(
        double coldThreshold,
        double warmExitThreshold,
        double warmEnterThreshold,
        double hotExitThreshold,
        double hotEnterThreshold,
        double superHotExitThreshold,
        double superHotEnterThreshold)
    {
        if (warmExitThreshold < coldThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warmExitThreshold),
                warmExitThreshold,
                "Warm exit threshold must be greater than or equal to cold threshold.");
        }

        if (warmEnterThreshold < warmExitThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warmEnterThreshold),
                warmEnterThreshold,
                "Warm enter threshold must be greater than or equal to warm exit threshold.");
        }

        if (hotExitThreshold < warmEnterThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hotExitThreshold),
                hotExitThreshold,
                "Hot exit threshold must be greater than or equal to warm enter threshold.");
        }

        if (hotEnterThreshold < hotExitThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hotEnterThreshold),
                hotEnterThreshold,
                "Hot enter threshold must be greater than or equal to hot exit threshold.");
        }

        if (superHotExitThreshold < hotEnterThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(superHotExitThreshold),
                superHotExitThreshold,
                "Super-hot exit threshold must be greater than or equal to hot enter threshold.");
        }

        if (superHotEnterThreshold < superHotExitThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(superHotEnterThreshold),
                superHotEnterThreshold,
                "Super-hot enter threshold must be greater than or equal to super-hot exit threshold.");
        }
    }
}
