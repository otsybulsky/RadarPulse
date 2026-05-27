namespace RadarPulse.Domain.Processing;

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

    public RadarProcessingHandlerDelta? Delta { get; }

    public string Diagnostic { get; }

    public bool IsSuccessful => Delta is not null;

    public static RadarProcessingHandlerDeltaSerializationResult Succeeded(
        RadarProcessingHandlerDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return new RadarProcessingHandlerDeltaSerializationResult(delta, string.Empty);
    }

    public static RadarProcessingHandlerDeltaSerializationResult Failed(
        string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        return new RadarProcessingHandlerDeltaSerializationResult(null, diagnostic);
    }
}
