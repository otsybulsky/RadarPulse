namespace RadarPulse.Domain.Streaming;

[Flags]
public enum DenseIdentityAllowedCharacters
{
    None = 0,
    UppercaseAsciiLetters = 1,
    Digits = 2,
    Underscore = 4
}
