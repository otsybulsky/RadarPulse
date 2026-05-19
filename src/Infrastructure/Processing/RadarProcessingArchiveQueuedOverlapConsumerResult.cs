using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingArchiveQueuedOverlapConsumerResult
{
    public RadarProcessingArchiveQueuedOverlapConsumerResult(
        RadarProcessingQueuedSessionResult sessionResult,
        TimeSpan elapsed = default)
    {
        ArgumentNullException.ThrowIfNull(sessionResult);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        SessionResult = sessionResult;
        Elapsed = elapsed;
    }

    public RadarProcessingQueuedSessionResult SessionResult { get; }

    public TimeSpan Elapsed { get; }

    public RadarProcessingQueuedSessionStatus Status => SessionResult.Status;

    public string Message => SessionResult.Message;

    public bool IsCompleted => SessionResult.IsCompleted;

    public bool IsFaulted => SessionResult.IsFaulted;

    public bool IsCanceled => SessionResult.IsCanceled;

    public bool IsDisposed => Status == RadarProcessingQueuedSessionStatus.Disposed;

    public static RadarProcessingArchiveQueuedOverlapConsumerResult Faulted(
        string message,
        TimeSpan elapsed = default) =>
        new(
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Faulted,
                message: message),
            elapsed);

    public static RadarProcessingArchiveQueuedOverlapConsumerResult Canceled(
        string message = "",
        TimeSpan elapsed = default) =>
        new(
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Canceled,
                message: message),
            elapsed);

    public static RadarProcessingArchiveQueuedOverlapConsumerResult Disposed(
        string message = "",
        TimeSpan elapsed = default) =>
        new(
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Disposed,
                message: message),
            elapsed);
}
