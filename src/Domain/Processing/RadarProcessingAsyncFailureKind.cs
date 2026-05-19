namespace RadarPulse.Domain.Processing;

public enum RadarProcessingAsyncFailureKind : byte
{
    None = 0,
    WorkerReportedFailure = 1,
    WorkerException = 2,
    DispatchRejected = 3,
    EnqueueRejected = 4,
    TimedOut = 5,
    WorkerGroupFaulted = 6
}
