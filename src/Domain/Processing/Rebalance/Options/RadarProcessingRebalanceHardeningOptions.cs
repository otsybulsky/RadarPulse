namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceHardeningOptions
{
    public static RadarProcessingRebalanceHardeningOptions Default { get; } = new();

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

    public RadarProcessingTelemetryRetentionOptions TelemetryRetention { get; }

    public RadarProcessingQuarantineLifecycleOptions QuarantineLifecycle { get; }

    public RadarProcessingValidationProfile ValidationProfile { get; }

    internal static void EnsureKnownValidationProfile(
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
