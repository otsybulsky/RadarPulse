using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing diagnostics and readiness posture for one processing run.
/// </summary>
public sealed class RadarProcessingRunDiagnosticsReadModel
{
    private readonly IReadOnlyList<string> warnings;

    /// <summary>
    /// Creates diagnostics from processing completeness, metrics, queue telemetry, readiness, and handler posture.
    /// </summary>
    public RadarProcessingRunDiagnosticsReadModel(
        bool processingCompletenessPassed,
        RadarProcessingMetrics metrics,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null,
        RadarProcessingDurableRuntimeReadinessSummary? readiness = null,
        IReadOnlyList<string>? warnings = null,
        RadarProcessingHandlerOutputProvenance handlerOutputProvenance =
            RadarProcessingHandlerOutputProvenance.HandlerFreeOrderedConcurrent,
        string handlerOutputBlockingReason = "")
    {
        EnsureKnownProvenance(handlerOutputProvenance);
        ArgumentNullException.ThrowIfNull(handlerOutputBlockingReason);

        ProcessingCompletenessPassed = processingCompletenessPassed;
        Metrics = metrics;
        QueueTelemetry = queueTelemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        Readiness = readiness ?? RadarProcessingDurableRuntimeReadinessSummary.Empty;
        HandlerOutputProvenance = handlerOutputProvenance;
        HandlerOutputBlockingReason = handlerOutputBlockingReason;
        this.warnings = CopyWarnings(warnings);
    }

    /// <summary>
    /// Indicates whether every accepted batch has successful processing evidence.
    /// </summary>
    public bool ProcessingCompletenessPassed { get; }

    /// <summary>
    /// Aggregate processing metrics.
    /// </summary>
    public RadarProcessingMetrics Metrics { get; }

    /// <summary>
    /// Provider queue telemetry.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }

    /// <summary>
    /// Durable runtime readiness summary.
    /// </summary>
    public RadarProcessingDurableRuntimeReadinessSummary Readiness { get; }

    /// <summary>
    /// Handler output provenance.
    /// </summary>
    public RadarProcessingHandlerOutputProvenance HandlerOutputProvenance { get; }

    /// <summary>
    /// First handler output blocking reason when handler output is blocked.
    /// </summary>
    public string HandlerOutputBlockingReason { get; }

    /// <summary>
    /// Indicates handler output came from ordered delta/merge.
    /// </summary>
    public bool UsesOrderedHandlerDeltaMerge =>
        HandlerOutputProvenance == RadarProcessingHandlerOutputProvenance.OrderedHandlerDeltaMerge;

    /// <summary>
    /// Indicates handler output used sequential snapshot fallback.
    /// </summary>
    public bool UsesSequentialHandlerFallback =>
        HandlerOutputProvenance == RadarProcessingHandlerOutputProvenance.StatefulSequentialFallback;

    /// <summary>
    /// Indicates handler output is blocked by an unsupported handler set.
    /// </summary>
    public bool HandlerOutputBlocked =>
        HandlerOutputProvenance == RadarProcessingHandlerOutputProvenance.UnsupportedHandlerSet;

    /// <summary>
    /// Indicates processing, durable readiness, and handler output posture are all ready.
    /// </summary>
    public bool IsReady =>
        ProcessingCompletenessPassed &&
        Readiness.IsReady &&
        !HandlerOutputBlocked;

    /// <summary>
    /// First blocking reason for run readiness.
    /// </summary>
    public string BlockingReason =>
        !ProcessingCompletenessPassed
            ? "processing completeness failed"
            : HandlerOutputBlocked
                ? HandlerOutputBlockingReason
            : Readiness.BlockingReason;

    /// <summary>
    /// Durable release failure count.
    /// </summary>
    public long ReleaseFailureCount => Readiness.ReleaseFailureCount;

    /// <summary>
    /// Terminal retained envelope count.
    /// </summary>
    public long TerminalRetainedEnvelopeCount => Readiness.TerminalRetainedEnvelopeCount;

    /// <summary>
    /// Terminal retained payload bytes.
    /// </summary>
    public long TerminalRetainedPayloadBytes => Readiness.TerminalRetainedPayloadBytes;

    /// <summary>
    /// Current combined retained batch count.
    /// </summary>
    public long CurrentCombinedRetainedBatchCount =>
        QueueTelemetry.CurrentCombinedRetainedBatchCount;

    /// <summary>
    /// Current combined retained payload bytes.
    /// </summary>
    public long CurrentCombinedRetainedPayloadBytes =>
        QueueTelemetry.CurrentCombinedRetainedPayloadBytes;

    /// <summary>
    /// Warnings captured for the read model.
    /// </summary>
    public IReadOnlyList<string> Warnings => warnings;

    internal static void EnsureKnownProvenance(
        RadarProcessingHandlerOutputProvenance handlerOutputProvenance)
    {
        if (handlerOutputProvenance is not RadarProcessingHandlerOutputProvenance.HandlerFreeOrderedConcurrent and
            not RadarProcessingHandlerOutputProvenance.StatefulSequentialFallback and
            not RadarProcessingHandlerOutputProvenance.OrderedHandlerDeltaMerge and
            not RadarProcessingHandlerOutputProvenance.UnsupportedHandlerSet)
        {
            throw new ArgumentOutOfRangeException(nameof(handlerOutputProvenance));
        }
    }

    private static IReadOnlyList<string> CopyWarnings(
        IReadOnlyList<string>? warnings)
    {
        if (warnings is null || warnings.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[warnings.Count];
        for (var i = 0; i < warnings.Count; i++)
        {
            var warning = warnings[i];
            ArgumentException.ThrowIfNullOrWhiteSpace(warning);
            result[i] = warning;
        }

        return Array.AsReadOnly(result);
    }
}
