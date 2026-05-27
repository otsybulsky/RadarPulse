namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQueuedProviderValidationContext
{
    public static RadarProcessingQueuedProviderValidationContext Default { get; } = new();

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

    public RadarProcessingQueuedProviderValidationSurface SemanticSurface { get; }

    public RadarProcessingQueuedProviderOverlapMode OverlapMode { get; }

    public RadarProcessingRetainedPayloadStrategy RetentionStrategy { get; }

    public RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry { get; }

    public TimeSpan OverlapElapsed { get; }

    public bool RequiresOverlapTelemetry => OverlapMode != RadarProcessingQueuedProviderOverlapMode.None;

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
