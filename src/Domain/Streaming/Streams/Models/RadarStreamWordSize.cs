namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Encoded gate value width for stream event payloads.
/// </summary>
public enum RadarStreamWordSize : byte
{
    /// <summary>
    /// One byte per gate.
    /// </summary>
    EightBit = 8,

    /// <summary>
    /// Two bytes per gate.
    /// </summary>
    SixteenBit = 16
}
