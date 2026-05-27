namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingPressureWindowOptions
{
    public static RadarProcessingPressureWindowOptions Default { get; } = new();

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

    public int SampleCapacity { get; }

    public int MinimumSampleCount { get; }

    public double ColdThreshold { get; }

    public double WarmExitThreshold { get; }

    public double WarmEnterThreshold { get; }

    public double HotExitThreshold { get; }

    public double HotEnterThreshold { get; }

    public double SuperHotExitThreshold { get; }

    public double SuperHotEnterThreshold { get; }

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
