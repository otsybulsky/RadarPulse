using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedPayloadRetentionResult
{
    private RadarProcessingRetainedPayloadRetentionResult(
        RadarProcessingRetainedPayloadRetentionStatus status,
        RadarProcessingRetainedPayloadStrategy strategy,
        RadarEventBatch? batch,
        RadarProcessingRetainedBatchResource? resource,
        TimeSpan elapsed,
        long allocatedBytes,
        string message)
    {
        EnsureKnownStatus(status);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        ArgumentNullException.ThrowIfNull(message);

        if (status == RadarProcessingRetainedPayloadRetentionStatus.Succeeded)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (batch.Lifetime != RadarEventBatchLifetime.Owned)
            {
                throw new ArgumentException("Successful retained payload results require owned batches.", nameof(batch));
            }

            resource ??= RadarProcessingRetainedBatchResource.NotRequired(strategy);
        }
        else if (batch is not null || resource is not null)
        {
            throw new ArgumentException("Failed retained payload results must not carry a batch or retained resource.", nameof(batch));
        }

        Status = status;
        Strategy = strategy;
        Batch = batch;
        Resource = resource;
        Elapsed = elapsed;
        AllocatedBytes = allocatedBytes;
        Message = message;

        if (batch is not null)
        {
            EventCount = batch.EventCount;
            PayloadBytes = batch.PayloadLength;
            if (batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum))
            {
                PayloadValueCount = payloadValueCount;
                RawValueChecksum = rawValueChecksum;
            }
        }
    }

    public RadarProcessingRetainedPayloadRetentionStatus Status { get; }

    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    public RadarEventBatch? Batch { get; }

    public RadarProcessingRetainedBatchResource? Resource { get; }

    public TimeSpan Elapsed { get; }

    public long AllocatedBytes { get; }

    public string Message { get; }

    public bool IsSuccessful => Status == RadarProcessingRetainedPayloadRetentionStatus.Succeeded;

    public int EventCount { get; }

    public int PayloadBytes { get; }

    public long PayloadValueCount { get; }

    public long RawValueChecksum { get; }

    public static RadarProcessingRetainedPayloadRetentionResult Succeeded(
        RadarProcessingRetainedPayloadStrategy strategy,
        RadarEventBatch batch,
        RadarProcessingRetainedBatchResource? resource = null,
        TimeSpan elapsed = default,
        long allocatedBytes = 0) =>
        new(
            RadarProcessingRetainedPayloadRetentionStatus.Succeeded,
            strategy,
            batch,
            resource,
            elapsed,
            allocatedBytes,
            string.Empty);

    public static RadarProcessingRetainedPayloadRetentionResult Succeeded(
        RadarProcessingRetainedPayloadStrategy strategy,
        RadarEventBatch batch,
        TimeSpan elapsed,
        long allocatedBytes = 0) =>
        Succeeded(strategy, batch, resource: null, elapsed, allocatedBytes);

    public static RadarProcessingRetainedPayloadRetentionResult UnsupportedStrategy(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy,
            strategy,
            message);

    public static RadarProcessingRetainedPayloadRetentionResult FailedCopy(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.FailedCopy,
            strategy,
            message);

    public static RadarProcessingRetainedPayloadRetentionResult Canceled(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.Canceled,
            strategy,
            message);

    public static RadarProcessingRetainedPayloadRetentionResult InvalidInput(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.InvalidInput,
            strategy,
            message);

    internal static void EnsureKnownStatus(
        RadarProcessingRetainedPayloadRetentionStatus status)
    {
        if (status is not RadarProcessingRetainedPayloadRetentionStatus.Succeeded and
            not RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy and
            not RadarProcessingRetainedPayloadRetentionStatus.FailedCopy and
            not RadarProcessingRetainedPayloadRetentionStatus.Canceled and
            not RadarProcessingRetainedPayloadRetentionStatus.InvalidInput)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static RadarProcessingRetainedPayloadRetentionResult Rejected(
        RadarProcessingRetainedPayloadRetentionStatus status,
        RadarProcessingRetainedPayloadStrategy strategy,
        string message) =>
        new(status, strategy, null, null, TimeSpan.Zero, allocatedBytes: 0, message);
}
