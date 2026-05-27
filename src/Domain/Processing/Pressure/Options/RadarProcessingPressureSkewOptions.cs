namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingPressureSkewOptions
{
    public static RadarProcessingPressureSkewOptions None { get; } = new();

    public RadarProcessingPressureSkewOptions(
        RadarProcessingPressureSkewProfile profile = RadarProcessingPressureSkewProfile.None,
        double factor = 1.0,
        int period = 8)
    {
        EnsureKnownProfile(profile);
        if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Skew factor must be finite and non-negative.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Profile = profile;
        Factor = factor;
        Period = period;
    }

    public RadarProcessingPressureSkewProfile Profile { get; }

    public double Factor { get; }

    public int Period { get; }

    public bool IsEnabled => Profile != RadarProcessingPressureSkewProfile.None && Factor > 0.0;

    internal static void EnsureKnownProfile(RadarProcessingPressureSkewProfile profile)
    {
        if (profile is not RadarProcessingPressureSkewProfile.None and
            not RadarProcessingPressureSkewProfile.HotShard and
            not RadarProcessingPressureSkewProfile.RotatingHotShard and
            not RadarProcessingPressureSkewProfile.HotPartition and
            not RadarProcessingPressureSkewProfile.TargetStarvation and
            not RadarProcessingPressureSkewProfile.BudgetStorm)
        {
            throw new ArgumentOutOfRangeException(nameof(profile));
        }
    }
}
