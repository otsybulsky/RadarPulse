namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedPayloadReleaseResult
{
    private RadarProcessingRetainedPayloadReleaseResult(
        RadarProcessingRetainedPayloadReleaseStatus status,
        RadarProcessingRetainedPayloadStrategy strategy,
        TimeSpan elapsed,
        long payloadBytes,
        string message)
    {
        EnsureKnownStatus(status);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Strategy = strategy;
        Elapsed = elapsed;
        PayloadBytes = payloadBytes;
        Message = message;
    }

    public RadarProcessingRetainedPayloadReleaseStatus Status { get; }

    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    public TimeSpan Elapsed { get; }

    public long PayloadBytes { get; }

    public string Message { get; }

    public bool IsReleased => Status == RadarProcessingRetainedPayloadReleaseStatus.Released;

    public bool IsFailure => Status == RadarProcessingRetainedPayloadReleaseStatus.Failed;

    public static RadarProcessingRetainedPayloadReleaseResult Released(
        RadarProcessingRetainedPayloadStrategy strategy,
        TimeSpan elapsed = default,
        long payloadBytes = 0) =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.Released,
            strategy,
            elapsed,
            payloadBytes,
            string.Empty);

    public static RadarProcessingRetainedPayloadReleaseResult AlreadyReleased(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased,
            strategy,
            TimeSpan.Zero,
            payloadBytes: 0,
            message);

    public static RadarProcessingRetainedPayloadReleaseResult Failed(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.Failed,
            strategy,
            TimeSpan.Zero,
            payloadBytes: 0,
            message);

    public static RadarProcessingRetainedPayloadReleaseResult NotRequired(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        new(
            RadarProcessingRetainedPayloadReleaseStatus.NotRequired,
            strategy,
            TimeSpan.Zero,
            payloadBytes: 0,
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
