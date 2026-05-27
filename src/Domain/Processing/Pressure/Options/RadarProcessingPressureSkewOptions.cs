namespace RadarPulse.Domain.Processing;

/// <summary>
/// Options for synthetic pressure skew applied before rebalance planning.
/// </summary>
public sealed record RadarProcessingPressureSkewOptions
{
    /// <summary>
    /// Disabled skew configuration.
    /// </summary>
    public static RadarProcessingPressureSkewOptions None { get; } = new();

    /// <summary>
    /// Creates pressure skew options.
    /// </summary>
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

    /// <summary>
    /// Skew profile to apply.
    /// </summary>
    public RadarProcessingPressureSkewProfile Profile { get; }

    /// <summary>
    /// Non-negative pressure multiplier used by the selected profile.
    /// </summary>
    public double Factor { get; }

    /// <summary>
    /// Evaluation period used by rotating profiles.
    /// </summary>
    public int Period { get; }

    /// <summary>
    /// Indicates whether skew should alter samples.
    /// </summary>
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
