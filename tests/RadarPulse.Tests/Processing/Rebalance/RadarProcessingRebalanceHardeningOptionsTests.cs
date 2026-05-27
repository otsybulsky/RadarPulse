using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceHardeningOptionsTests
{
    [Fact]
    public void DefaultHardeningOptionsUseBoundedRecentDiagnosticSettings()
    {
        var options = RadarProcessingRebalanceHardeningOptions.Default;

        Assert.Same(RadarProcessingTelemetryRetentionOptions.Default, options.TelemetryRetention);
        Assert.Same(RadarProcessingQuarantineLifecycleOptions.Default, options.QuarantineLifecycle);
        Assert.Equal(RadarProcessingValidationProfile.Diagnostic, options.ValidationProfile);

        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Recent, options.TelemetryRetention.RetentionMode);
        Assert.Equal(128, options.TelemetryRetention.MaxRetainedDecisions);
        Assert.Equal(64, options.TelemetryRetention.MaxRetainedLifecycleTransitions);
        Assert.Equal(64, options.TelemetryRetention.MaxRetainedAcceptedMoves);
        Assert.Equal(32, options.TelemetryRetention.MaxRetainedValidationFailures);
        Assert.Equal(128, options.TelemetryRetention.MaxRetainedWorkerBatches);
        Assert.Equal(64, options.TelemetryRetention.MaxRetainedWorkerFailures);

        Assert.Equal(64, options.QuarantineLifecycle.QuarantineTtlEvaluations);
        Assert.Equal(3, options.QuarantineLifecycle.SustainedCoolingSampleCount);
        Assert.Equal(0.25, options.QuarantineLifecycle.MaterialPressureChangeThreshold);
    }

    [Fact]
    public void ValidationProfileEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingValidationProfile.Off);
        Assert.Equal(1, (int)RadarProcessingValidationProfile.Essential);
        Assert.Equal(2, (int)RadarProcessingValidationProfile.Diagnostic);
        Assert.Equal(3, (int)RadarProcessingValidationProfile.Benchmark);
    }

    [Fact]
    public void RetentionModeEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingDiagnosticRetentionMode.Counters);
        Assert.Equal(1, (int)RadarProcessingDiagnosticRetentionMode.Recent);
        Assert.Equal(2, (int)RadarProcessingDiagnosticRetentionMode.Diagnostic);
    }

    [Fact]
    public void HardeningOptionsComposeIndependentRetentionAndValidationSettings()
    {
        var retention = new RadarProcessingTelemetryRetentionOptions(
            RadarProcessingDiagnosticRetentionMode.Counters,
            maxRetainedDecisions: 0,
            maxRetainedLifecycleTransitions: 0,
            maxRetainedAcceptedMoves: 0,
            maxRetainedValidationFailures: 0);
        var lifecycle = new RadarProcessingQuarantineLifecycleOptions(
            quarantineTtlEvaluations: 12,
            sustainedCoolingSampleCount: 4,
            materialPressureChangeThreshold: 0.5);

        var options = new RadarProcessingRebalanceHardeningOptions(
            retention,
            lifecycle,
            RadarProcessingValidationProfile.Benchmark);

        Assert.Same(retention, options.TelemetryRetention);
        Assert.Same(lifecycle, options.QuarantineLifecycle);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Counters, options.TelemetryRetention.RetentionMode);
        Assert.Equal(0, options.TelemetryRetention.MaxRetainedDecisions);
        Assert.Equal(RadarProcessingValidationProfile.Benchmark, options.ValidationProfile);
        Assert.Equal(12, options.QuarantineLifecycle.QuarantineTtlEvaluations);
        Assert.Equal(4, options.QuarantineLifecycle.SustainedCoolingSampleCount);
        Assert.Equal(0.5, options.QuarantineLifecycle.MaterialPressureChangeThreshold);
    }

    [Fact]
    public void HardeningOptionsRejectInvalidValidationProfile()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceHardeningOptions(
                validationProfile: (RadarProcessingValidationProfile)255));
    }

    [Fact]
    public void RetentionOptionsRejectInvalidModeAndLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions((RadarProcessingDiagnosticRetentionMode)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions(maxRetainedDecisions: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions(maxRetainedLifecycleTransitions: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions(maxRetainedAcceptedMoves: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions(maxRetainedValidationFailures: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions(maxRetainedWorkerBatches: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingTelemetryRetentionOptions(maxRetainedWorkerFailures: -1));
    }

    [Fact]
    public void RetentionOptionsAllowCountersOnlyZeroDetailLimits()
    {
        var options = new RadarProcessingTelemetryRetentionOptions(
            RadarProcessingDiagnosticRetentionMode.Counters,
            maxRetainedDecisions: 0,
            maxRetainedLifecycleTransitions: 0,
            maxRetainedAcceptedMoves: 0,
            maxRetainedValidationFailures: 0,
            maxRetainedWorkerBatches: 0,
            maxRetainedWorkerFailures: 0);

        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Counters, options.RetentionMode);
        Assert.Equal(0, options.MaxRetainedDecisions);
        Assert.Equal(0, options.MaxRetainedLifecycleTransitions);
        Assert.Equal(0, options.MaxRetainedAcceptedMoves);
        Assert.Equal(0, options.MaxRetainedValidationFailures);
        Assert.Equal(0, options.MaxRetainedWorkerBatches);
        Assert.Equal(0, options.MaxRetainedWorkerFailures);
    }

    [Fact]
    public void QuarantineLifecycleOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQuarantineLifecycleOptions(quarantineTtlEvaluations: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQuarantineLifecycleOptions(sustainedCoolingSampleCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQuarantineLifecycleOptions(materialPressureChangeThreshold: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQuarantineLifecycleOptions(materialPressureChangeThreshold: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQuarantineLifecycleOptions(materialPressureChangeThreshold: double.PositiveInfinity));
    }
}
