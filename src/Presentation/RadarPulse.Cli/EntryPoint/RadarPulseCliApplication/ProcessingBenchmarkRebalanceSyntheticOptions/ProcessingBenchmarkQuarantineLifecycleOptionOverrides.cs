using RadarPulse.Domain.Processing;

public sealed record ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
    int? QuarantineTtlEvaluations,
    int? SustainedCoolingSampleCount,
    double? MaterialPressureChangeThreshold)
{
    /// <summary>
    /// Gets an override set with no overridden lifecycle values.
    /// </summary>
    public static ProcessingBenchmarkQuarantineLifecycleOptionOverrides None { get; } = new(null, null, null);

    /// <summary>
    /// Gets whether at least one lifecycle option was explicitly supplied.
    /// </summary>
    public bool HasOverrides =>
        QuarantineTtlEvaluations is not null ||
        SustainedCoolingSampleCount is not null ||
        MaterialPressureChangeThreshold is not null;

    /// <summary>
    /// Applies configured overrides to a baseline lifecycle option set.
    /// </summary>
    public RadarProcessingQuarantineLifecycleOptions ApplyTo(
        RadarProcessingQuarantineLifecycleOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (!HasOverrides)
        {
            return baseline;
        }

        return new RadarProcessingQuarantineLifecycleOptions(
            QuarantineTtlEvaluations ?? baseline.QuarantineTtlEvaluations,
            SustainedCoolingSampleCount ?? baseline.SustainedCoolingSampleCount,
            MaterialPressureChangeThreshold ?? baseline.MaterialPressureChangeThreshold);
    }
}
