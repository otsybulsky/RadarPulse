namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPressureScore
{
    public RadarProcessingPressureScore(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Pressure score must be finite and non-negative.");
        }

        Value = value;
    }

    public double Value { get; }

    public static RadarProcessingPressureScore Zero => default;
}
