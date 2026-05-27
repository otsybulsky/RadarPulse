namespace RadarPulse.Domain.Processing;

public enum RadarProcessingAsyncWorkStatus : byte
{
    Succeeded = 1,
    Failed = 2,
    Canceled = 3
}
