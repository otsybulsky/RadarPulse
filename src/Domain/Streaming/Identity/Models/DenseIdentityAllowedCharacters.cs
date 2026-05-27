namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Allowed ASCII character groups for dense identity text.
/// </summary>
[Flags]
public enum DenseIdentityAllowedCharacters
{
    /// <summary>
    /// No characters are allowed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Uppercase ASCII letters A through Z.
    /// </summary>
    UppercaseAsciiLetters = 1,

    /// <summary>
    /// ASCII digits 0 through 9.
    /// </summary>
    Digits = 2,

    /// <summary>
    /// ASCII underscore.
    /// </summary>
    Underscore = 4
}
