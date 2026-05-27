namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of releasing a retained payload resource.
/// </summary>
public sealed record RadarProcessingRetainedPayloadReleaseResult
{
    private RadarProcessingRetainedPayloadReleaseResult(
        RadarProcessingRetainedPayloadReleaseStatus status,
        RadarProcessingRetainedPayloadStrategy strategy,
        TimeSpan elapsed,
        long payloadBytes,
        long poolReturnCount,
        long eventPoolReturnCount,
        long payloadPoolReturnCount,
        string message)
    {
        EnsureKnownStatus(status);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(poolReturnCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventPoolReturnCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadPoolReturnCount);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Strategy = strategy;
        Elapsed = elapsed;
        PayloadBytes = payloadBytes;
        PoolReturnCount = poolReturnCount;
        EventPoolReturnCount = eventPoolReturnCount;
        PayloadPoolReturnCount = payloadPoolReturnCount;
        Message = message;
    }

    /// <summary>
    /// Release outcome status.
    /// </summary>
    public RadarProcessingRetainedPayloadReleaseStatus Status { get; }

    /// <summary>
    /// Strategy that created the retained resource.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    /// <summary>
    /// Time spent releasing the resource.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Payload bytes represented by the released resource.
    /// </summary>
    public long PayloadBytes { get; }

    /// <summary>
    /// Total pool return count.
    /// </summary>
    public long PoolReturnCount { get; }

    /// <summary>
    /// Event buffer pool return count.
    /// </summary>
    public long EventPoolReturnCount { get; }

    /// <summary>
    /// Payload buffer pool return count.
    /// </summary>
    public long PayloadPoolReturnCount { get; }

    /// <summary>
    /// Diagnostic message for failed or repeated release.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the resource was released by this result.
    /// </summary>
    public bool IsReleased => Status == RadarProcessingRetainedPayloadReleaseStatus.Released;

    /// <summary>
    /// Indicates whether release failed.
    /// </summary>
    public bool IsFailure => Status == RadarProcessingRetainedPayloadReleaseStatus.Failed;

    /// <summary>
    /// Creates a successful release result.
    /// </summary>
    public static RadarProcessingRetainedPayloadReleaseResult Released(
        RadarProcessingRetainedPayloadStrategy strategy,
        TimeSpan elapsed = default,
        long payloadBytes = 0,
        long poolReturnCount = 0,
        long eventPoolReturnCount = 0,
        long payloadPoolReturnCount = 0) =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.Released,
            strategy,
            elapsed,
            payloadBytes,
            poolReturnCount,
            eventPoolReturnCount,
            payloadPoolReturnCount,
            string.Empty);

    /// <summary>
    /// Creates a result for a resource that was already released.
    /// </summary>
    public static RadarProcessingRetainedPayloadReleaseResult AlreadyReleased(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased,
            strategy,
            TimeSpan.Zero,
            payloadBytes: 0,
            poolReturnCount: 0,
            eventPoolReturnCount: 0,
            payloadPoolReturnCount: 0,
            message);

    /// <summary>
    /// Creates a failed release result.
    /// </summary>
    public static RadarProcessingRetainedPayloadReleaseResult Failed(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.Failed,
            strategy,
            TimeSpan.Zero,
            payloadBytes: 0,
            poolReturnCount: 0,
            eventPoolReturnCount: 0,
            payloadPoolReturnCount: 0,
            message);

    /// <summary>
    /// Creates a result for strategies that require no release action.
    /// </summary>
    public static RadarProcessingRetainedPayloadReleaseResult NotRequired(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.NotRequired,
            strategy,
            TimeSpan.Zero,
            payloadBytes: 0,
            poolReturnCount: 0,
            eventPoolReturnCount: 0,
            payloadPoolReturnCount: 0,
            message);

    internal static void EnsureKnownStatus(
        RadarProcessingRetainedPayloadReleaseStatus status)
    {
        if (status is not RadarProcessingRetainedPayloadReleaseStatus.Released and
            not RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased and
            not RadarProcessingRetainedPayloadReleaseStatus.Failed and
            not RadarProcessingRetainedPayloadReleaseStatus.NotRequired)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
