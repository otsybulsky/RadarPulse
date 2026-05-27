namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Dense identity validation failure category.
/// </summary>
public enum DenseIdentityValidationError
{
    /// <summary>
    /// No validation error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Input was empty.
    /// </summary>
    Empty = 1,

    /// <summary>
    /// Input was shorter than the policy minimum.
    /// </summary>
    TooShort = 2,

    /// <summary>
    /// Input was longer than the policy maximum.
    /// </summary>
    TooLong = 3,

    /// <summary>
    /// Input contained a character outside the allowed ASCII set.
    /// </summary>
    InvalidCharacter = 4
}
