namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of deserializing a persisted handler delta.
/// </summary>
public sealed class RadarProcessingHandlerDeltaSerializationResult
{
    private RadarProcessingHandlerDeltaSerializationResult(
        RadarProcessingHandlerDelta? delta,
        string diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        Delta = delta;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Deserialized delta when successful.
    /// </summary>
    public RadarProcessingHandlerDelta? Delta { get; }

    /// <summary>
    /// Diagnostic message when deserialization failed.
    /// </summary>
    public string Diagnostic { get; }

    /// <summary>
    /// Indicates whether a valid delta was produced.
    /// </summary>
    public bool IsSuccessful => Delta is not null;

    /// <summary>
    /// Creates a successful deserialization result.
    /// </summary>
    public static RadarProcessingHandlerDeltaSerializationResult Succeeded(
        RadarProcessingHandlerDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return new RadarProcessingHandlerDeltaSerializationResult(delta, string.Empty);
    }

    /// <summary>
    /// Creates a failed deserialization result.
    /// </summary>
    public static RadarProcessingHandlerDeltaSerializationResult Failed(
        string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        return new RadarProcessingHandlerDeltaSerializationResult(null, diagnostic);
    }
}
