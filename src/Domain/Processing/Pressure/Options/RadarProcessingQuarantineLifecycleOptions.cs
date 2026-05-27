namespace RadarPulse.Domain.Processing;

/// <summary>
/// Policy options for quarantined partition retry and clearing behavior.
/// </summary>
public sealed record RadarProcessingQuarantineLifecycleOptions
{
    /// <summary>
    /// Default quarantine lifecycle policy.
    /// </summary>
    public static RadarProcessingQuarantineLifecycleOptions Default { get; } = new();

    /// <summary>
    /// Creates quarantine lifecycle options.
    /// </summary>
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

    /// <summary>
    /// Evaluations a partition may remain quarantined before retry eligibility.
    /// </summary>
    public int QuarantineTtlEvaluations { get; }

    /// <summary>
    /// Consecutive cooling samples required to clear quarantine.
    /// </summary>
    public int SustainedCoolingSampleCount { get; }

    /// <summary>
    /// Relative pressure change required to mark quarantine retry-eligible.
    /// </summary>
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
