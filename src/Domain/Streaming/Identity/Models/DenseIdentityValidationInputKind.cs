namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Input representation used for dense identity validation diagnostics.
/// </summary>
public enum DenseIdentityValidationInputKind
{
    /// <summary>
    /// UTF-16 text input.
    /// </summary>
    Text = 0,

    /// <summary>
    /// UTF-8 byte input.
    /// </summary>
    Utf8Bytes = 1
}
