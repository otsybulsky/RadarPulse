namespace RadarPulse.Domain.Processing;

/// <summary>
/// Identifies an async processing worker within a worker group.
/// </summary>
public readonly record struct RadarProcessingWorkerId
{
    /// <summary>
    /// Creates a worker id from a non-negative worker index.
    /// </summary>
    public RadarProcessingWorkerId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        Value = value;
    }

    /// <summary>
    /// Gets the zero-based worker index.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Returns the worker index as a string for telemetry labels.
    /// </summary>
    public override string ToString() => Value.ToString();
}
