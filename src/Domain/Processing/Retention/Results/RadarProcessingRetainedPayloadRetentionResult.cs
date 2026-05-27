using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of retaining queued payload ownership.
/// </summary>
/// <remarks>
/// Successful results always carry an owned batch and a retained resource. Failed
/// results carry no batch or resource so callers cannot accidentally process a
/// payload whose ownership was not retained.
/// </remarks>
public sealed record RadarProcessingRetainedPayloadRetentionResult
{
    private RadarProcessingRetainedPayloadRetentionResult(
        RadarProcessingRetainedPayloadRetentionStatus status,
        RadarProcessingRetainedPayloadStrategy strategy,
        RadarEventBatch? batch,
        RadarProcessingRetainedBatchResource? resource,
        TimeSpan elapsed,
        long allocatedBytes,
        long poolRentCount,
        long poolMissCount,
        long eventPoolRentCount,
        long payloadPoolRentCount,
        long eventPoolMissCount,
        long payloadPoolMissCount,
        string message)
    {
        EnsureKnownStatus(status);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(poolRentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(poolMissCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventPoolRentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadPoolRentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventPoolMissCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadPoolMissCount);
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
        PoolRentCount = poolRentCount;
        PoolMissCount = poolMissCount;
        EventPoolRentCount = eventPoolRentCount;
        PayloadPoolRentCount = payloadPoolRentCount;
        EventPoolMissCount = eventPoolMissCount;
        PayloadPoolMissCount = payloadPoolMissCount;
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

    /// <summary>
    /// Retention outcome status.
    /// </summary>
    public RadarProcessingRetainedPayloadRetentionStatus Status { get; }

    /// <summary>
    /// Strategy used for the retention attempt.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    /// <summary>
    /// Owned batch produced by a successful retention attempt.
    /// </summary>
    public RadarEventBatch? Batch { get; }

    /// <summary>
    /// Retained payload resource associated with a successful attempt.
    /// </summary>
    public RadarProcessingRetainedBatchResource? Resource { get; }

    /// <summary>
    /// Time spent retaining payload ownership.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Bytes allocated during retention.
    /// </summary>
    public long AllocatedBytes { get; }

    /// <summary>
    /// Total pool rent count.
    /// </summary>
    public long PoolRentCount { get; }

    /// <summary>
    /// Total pool miss count.
    /// </summary>
    public long PoolMissCount { get; }

    /// <summary>
    /// Event buffer pool rent count.
    /// </summary>
    public long EventPoolRentCount { get; }

    /// <summary>
    /// Payload buffer pool rent count.
    /// </summary>
    public long PayloadPoolRentCount { get; }

    /// <summary>
    /// Event buffer pool miss count.
    /// </summary>
    public long EventPoolMissCount { get; }

    /// <summary>
    /// Payload buffer pool miss count.
    /// </summary>
    public long PayloadPoolMissCount { get; }

    /// <summary>
    /// Diagnostic message for failed retention.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether retention succeeded.
    /// </summary>
    public bool IsSuccessful => Status == RadarProcessingRetainedPayloadRetentionStatus.Succeeded;

    /// <summary>
    /// Event count retained from the successful batch.
    /// </summary>
    public int EventCount { get; }

    /// <summary>
    /// Payload byte length retained from the successful batch.
    /// </summary>
    public int PayloadBytes { get; }

    /// <summary>
    /// Payload value count retained from batch metrics.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Raw payload checksum retained from batch metrics.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Creates a successful retention result with full telemetry.
    /// </summary>
    public static RadarProcessingRetainedPayloadRetentionResult Succeeded(
        RadarProcessingRetainedPayloadStrategy strategy,
        RadarEventBatch batch,
        RadarProcessingRetainedBatchResource? resource = null,
        TimeSpan elapsed = default,
        long allocatedBytes = 0,
        long poolRentCount = 0,
        long poolMissCount = 0,
        long eventPoolRentCount = 0,
        long payloadPoolRentCount = 0,
        long eventPoolMissCount = 0,
        long payloadPoolMissCount = 0) =>
        new(
            RadarProcessingRetainedPayloadRetentionStatus.Succeeded,
            strategy,
            batch,
            resource,
            elapsed,
            allocatedBytes,
            poolRentCount,
            poolMissCount,
            eventPoolRentCount,
            payloadPoolRentCount,
            eventPoolMissCount,
            payloadPoolMissCount,
            string.Empty);

    /// <summary>
    /// Creates a successful retention result with elapsed time and allocation bytes.
    /// </summary>
    public static RadarProcessingRetainedPayloadRetentionResult Succeeded(
        RadarProcessingRetainedPayloadStrategy strategy,
        RadarEventBatch batch,
        TimeSpan elapsed,
        long allocatedBytes = 0) =>
        Succeeded(strategy, batch, resource: null, elapsed, allocatedBytes);

    /// <summary>
    /// Creates a failed result for an unsupported strategy.
    /// </summary>
    public static RadarProcessingRetainedPayloadRetentionResult UnsupportedStrategy(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy,
            strategy,
            message);

    /// <summary>
    /// Creates a failed result for a payload copy failure.
    /// </summary>
    public static RadarProcessingRetainedPayloadRetentionResult FailedCopy(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.FailedCopy,
            strategy,
            message);

    /// <summary>
    /// Creates a canceled retention result.
    /// </summary>
    public static RadarProcessingRetainedPayloadRetentionResult Canceled(
        RadarProcessingRetainedPayloadStrategy strategy,
        string message = "") =>
        Rejected(
            RadarProcessingRetainedPayloadRetentionStatus.Canceled,
            strategy,
            message);

    /// <summary>
    /// Creates a failed result for invalid input.
    /// </summary>
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
        new(
            status,
            strategy,
            null,
            null,
            TimeSpan.Zero,
            allocatedBytes: 0,
            poolRentCount: 0,
            poolMissCount: 0,
            eventPoolRentCount: 0,
            payloadPoolRentCount: 0,
            eventPoolMissCount: 0,
            payloadPoolMissCount: 0,
            message);
}
