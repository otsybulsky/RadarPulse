namespace RadarPulse.Domain.Processing;

/// <summary>
/// Diagnostic and lifecycle hardening options for rebalance sessions.
/// </summary>
/// <remarks>
/// Hardening does not change the accepted local demo topology boundary. It
/// controls how much rebalance evidence is retained, how quarantine lifecycle
/// transitions are interpreted, and how strict session validation should be.
/// </remarks>
public sealed record RadarProcessingRebalanceHardeningOptions
{
    /// <summary>
    /// Default diagnostic hardening configuration.
    /// </summary>
    public static RadarProcessingRebalanceHardeningOptions Default { get; } = new();

    /// <summary>
    /// Creates rebalance hardening options.
    /// </summary>
    public RadarProcessingRebalanceHardeningOptions(
        RadarProcessingTelemetryRetentionOptions? telemetryRetention = null,
        RadarProcessingQuarantineLifecycleOptions? quarantineLifecycle = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        EnsureKnownValidationProfile(validationProfile);

        TelemetryRetention = telemetryRetention ?? RadarProcessingTelemetryRetentionOptions.Default;
        QuarantineLifecycle = quarantineLifecycle ?? RadarProcessingQuarantineLifecycleOptions.Default;
        ValidationProfile = validationProfile;
    }

    /// <summary>
    /// Retention policy for rebalance telemetry detail windows.
    /// </summary>
    public RadarProcessingTelemetryRetentionOptions TelemetryRetention { get; }

    /// <summary>
    /// Policy for quarantine entry, retry, reentry, and clearing transitions.
    /// </summary>
    public RadarProcessingQuarantineLifecycleOptions QuarantineLifecycle { get; }

    /// <summary>
    /// Validation depth applied to produced rebalance session results.
    /// </summary>
    public RadarProcessingValidationProfile ValidationProfile { get; }

    /// <summary>
    /// Throws when a rebalance validation profile is outside the known contract.
    /// </summary>
    public static void EnsureKnownValidationProfile(
        RadarProcessingValidationProfile validationProfile)
    {
        if (validationProfile is not RadarProcessingValidationProfile.Off and
            not RadarProcessingValidationProfile.Essential and
            not RadarProcessingValidationProfile.Diagnostic and
            not RadarProcessingValidationProfile.Benchmark)
        {
            throw new ArgumentOutOfRangeException(nameof(validationProfile));
        }
    }
}
