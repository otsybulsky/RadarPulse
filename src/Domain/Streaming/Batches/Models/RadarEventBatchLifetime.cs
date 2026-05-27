namespace RadarPulse.Domain.Streaming;

public enum RadarEventBatchLifetime : byte
{
    Owned = 0,
    Leased = 1
}
