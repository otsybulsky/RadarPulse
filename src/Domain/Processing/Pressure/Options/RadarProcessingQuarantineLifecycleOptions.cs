namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQuarantineLifecycleOptions
{
    public static RadarProcessingQuarantineLifecycleOptions Default { get; } = new();

    public RadarProcessingQuarantineLifecycleOptions(
        int quarantineTtlEvaluations = 64,
        int sustainedCoolingSampleCount = 3,
        double materialPressureChangeThreshold = 0.25)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quarantineTtlEvaluations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sustainedCoolingSampleCount);
        ThrowIfInvalidThreshold(materialPressureChangeThreshold, nameof(materialPressureChangeThreshold));

        QuarantineTtlEvaluations = quarantineTtlEvaluations;
        SustainedCoolingSampleCount = sustainedCoolingSampleCount;
        MaterialPressureChangeThreshold = materialPressureChangeThreshold;
    }

    public int QuarantineTtlEvaluations { get; }

    public int SustainedCoolingSampleCount { get; }

    public double MaterialPressureChangeThreshold { get; }

    private static void ThrowIfInvalidThreshold(
        double value,
        string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Threshold must be finite and non-negative.");
        }
    }
}
