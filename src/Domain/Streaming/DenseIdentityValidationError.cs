namespace RadarPulse.Domain.Streaming;

public enum DenseIdentityValidationError
{
    None = 0,
    Empty = 1,
    TooShort = 2,
    TooLong = 3,
    InvalidCharacter = 4
}
