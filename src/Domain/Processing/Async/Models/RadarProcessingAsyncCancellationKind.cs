namespace RadarPulse.Domain.Processing;

public enum RadarProcessingAsyncCancellationKind : byte
{
    None = 0,
    BeforeDispatch = 1,
    WhileQueued = 2,
    WhileRunning = 3,
    Timeout = 4,
    Mixed = 5
}
