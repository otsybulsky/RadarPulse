namespace RadarPulse.Domain.Processing;

/// <summary>
/// Context that defines the semantic surface and runtime contour for queued-provider validation.
/// </summary>
/// <remarks>
/// Validation depends on more than result lists: overlap mode, retention strategy,
/// retention telemetry, and semantic surface decide which invariants are required
/// for a candidate queued-provider run.
/// </remarks>
public sealed record RadarProcessingQueuedProviderValidationContext
{
    /// <summary>
    /// Default processing-only validation context without overlap.
    /// </summary>
    public static RadarProcessingQueuedProviderValidationContext Default { get; } = new();

    /// <summary>
    /// Creates validation context with a consistent retention strategy and telemetry pair.
    /// </summary>
    public RadarProcessingQueuedProviderValidationContext(
        RadarProcessingQueuedProviderValidationSurface semanticSurface =
            RadarProcessingQueuedProviderValidationSurface.ProcessingOnly,
        RadarProcessingQueuedProviderOverlapMode overlapMode = RadarProcessingQueuedProviderOverlapMode.None,
        RadarProcessingRetainedPayloadStrategy retentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
        RadarProcessingRetainedPayloadTelemetrySummary? retentionTelemetry = null,
        TimeSpan overlapElapsed = default)
    {
        EnsureKnownSurface(semanticSurface);
        EnsureKnownOverlapMode(overlapMode);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(retentionStrategy);
        if (overlapElapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapElapsed));
        }

        var effectiveRetentionTelemetry = retentionTelemetry ?? RadarProcessingRetainedPayloadTelemetrySummary.Empty;
        if (!ReferenceEquals(effectiveRetentionTelemetry, RadarProcessingRetainedPayloadTelemetrySummary.Empty) &&
            effectiveRetentionTelemetry.Strategy != retentionStrategy)
        {
            throw new ArgumentException(
                "Retention telemetry strategy must match the validation retention strategy.",
                nameof(retentionTelemetry));
        }

        SemanticSurface = semanticSurface;
        OverlapMode = overlapMode;
        RetentionStrategy = retentionStrategy;
        RetentionTelemetry = effectiveRetentionTelemetry;
        OverlapElapsed = overlapElapsed;
    }

    /// <summary>
    /// Semantic validation surface.
    /// </summary>
    public RadarProcessingQueuedProviderValidationSurface SemanticSurface { get; }

    /// <summary>
    /// Producer/consumer overlap mode used by the run being validated.
    /// </summary>
    public RadarProcessingQueuedProviderOverlapMode OverlapMode { get; }

    /// <summary>
    /// Retained payload strategy used by the run being validated.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy RetentionStrategy { get; }

    /// <summary>
    /// Retention telemetry captured for the run being validated.
    /// </summary>
    public RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry { get; }

    /// <summary>
    /// Elapsed overlap time captured for producer/consumer mode.
    /// </summary>
    public TimeSpan OverlapElapsed { get; }

    /// <summary>
    /// Indicates whether validation must see overlap telemetry.
    /// </summary>
    public bool RequiresOverlapTelemetry => OverlapMode != RadarProcessingQueuedProviderOverlapMode.None;

    /// <summary>
    /// Indicates whether non-empty retention telemetry was supplied.
    /// </summary>
    public bool HasRetentionTelemetry =>
        !ReferenceEquals(RetentionTelemetry, RadarProcessingRetainedPayloadTelemetrySummary.Empty) ||
        RetentionTelemetry.RetentionAttemptCount > 0 ||
        RetentionTelemetry.RetainedBatchCount > 0 ||
        RetentionTelemetry.ReleaseAttemptCount > 0;

    internal static void EnsureKnownSurface(
        RadarProcessingQueuedProviderValidationSurface semanticSurface)
    {
        if (semanticSurface is not RadarProcessingQueuedProviderValidationSurface.ProcessingOnly and
            not RadarProcessingQueuedProviderValidationSurface.Rebalance)
        {
            throw new ArgumentOutOfRangeException(nameof(semanticSurface));
        }
    }

    internal static void EnsureKnownOverlapMode(
        RadarProcessingQueuedProviderOverlapMode overlapMode)
    {
        if (overlapMode is not RadarProcessingQueuedProviderOverlapMode.None and
            not RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapMode));
        }
    }
}
