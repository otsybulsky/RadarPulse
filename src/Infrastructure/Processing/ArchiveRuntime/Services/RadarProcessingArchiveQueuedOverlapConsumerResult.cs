using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Consumer-side result from draining queued archive batches into processing.
/// </summary>
public sealed class RadarProcessingArchiveQueuedOverlapConsumerResult
{
    /// <summary>
    /// Creates a consumer result from a queued session result.
    /// </summary>
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

    /// <summary>
    /// Queued processing or rebalance session result.
    /// </summary>
    public RadarProcessingQueuedSessionResult SessionResult { get; }

    /// <summary>
    /// Consumer elapsed time.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Queued session terminal status.
    /// </summary>
    public RadarProcessingQueuedSessionStatus Status => SessionResult.Status;

    /// <summary>
    /// Queued session diagnostic message.
    /// </summary>
    public string Message => SessionResult.Message;

    /// <summary>
    /// Indicates whether the consumer completed.
    /// </summary>
    public bool IsCompleted => SessionResult.IsCompleted;

    /// <summary>
    /// Indicates whether the consumer faulted.
    /// </summary>
    public bool IsFaulted => SessionResult.IsFaulted;

    /// <summary>
    /// Indicates whether the consumer was canceled.
    /// </summary>
    public bool IsCanceled => SessionResult.IsCanceled;

    /// <summary>
    /// Indicates whether the consumer observed disposal.
    /// </summary>
    public bool IsDisposed => Status == RadarProcessingQueuedSessionStatus.Disposed;

    /// <summary>
    /// Creates a faulted consumer result without a completed session.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapConsumerResult Faulted(
        string message,
        TimeSpan elapsed = default) =>
        new(
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Faulted,
                message: message),
            elapsed);

    /// <summary>
    /// Creates a canceled consumer result without a completed session.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapConsumerResult Canceled(
        string message = "",
        TimeSpan elapsed = default) =>
        new(
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Canceled,
                message: message),
            elapsed);

    /// <summary>
    /// Creates a disposed consumer result without a completed session.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapConsumerResult Disposed(
        string message = "",
        TimeSpan elapsed = default) =>
        new(
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Disposed,
                message: message),
            elapsed);
}
