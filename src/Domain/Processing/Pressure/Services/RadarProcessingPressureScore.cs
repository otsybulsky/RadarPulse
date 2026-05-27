namespace RadarPulse.Domain.Processing;

/// <summary>
/// Non-negative scalar pressure value derived from route metrics.
/// </summary>
public readonly record struct RadarProcessingPressureScore
{
    /// <summary>
    /// Creates a pressure score from a finite non-negative value.
    /// </summary>
    public RadarProcessingPressureScore(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Pressure score must be finite and non-negative.");
        }

        Value = value;
    }

    /// <summary>
    /// Numeric pressure value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Zero pressure score.
    /// </summary>
    public static RadarProcessingPressureScore Zero => default;
}
