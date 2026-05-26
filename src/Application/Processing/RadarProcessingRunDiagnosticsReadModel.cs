using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingRunDiagnosticsReadModel
{
    private readonly IReadOnlyList<string> warnings;

    public RadarProcessingRunDiagnosticsReadModel(
        bool processingCompletenessPassed,
        RadarProcessingMetrics metrics,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null,
        RadarProcessingDurableRuntimeReadinessSummary? readiness = null,
        IReadOnlyList<string>? warnings = null)
    {
        ProcessingCompletenessPassed = processingCompletenessPassed;
        Metrics = metrics;
        QueueTelemetry = queueTelemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        Readiness = readiness ?? RadarProcessingDurableRuntimeReadinessSummary.Empty;
        this.warnings = CopyWarnings(warnings);
    }

    public bool ProcessingCompletenessPassed { get; }

    public RadarProcessingMetrics Metrics { get; }

    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }

    public RadarProcessingDurableRuntimeReadinessSummary Readiness { get; }

    public bool IsReady => ProcessingCompletenessPassed && Readiness.IsReady;

    public string BlockingReason =>
        !ProcessingCompletenessPassed
            ? "processing completeness failed"
            : Readiness.BlockingReason;

    public long ReleaseFailureCount => Readiness.ReleaseFailureCount;

    public long TerminalRetainedEnvelopeCount => Readiness.TerminalRetainedEnvelopeCount;

    public long TerminalRetainedPayloadBytes => Readiness.TerminalRetainedPayloadBytes;

    public long CurrentCombinedRetainedBatchCount =>
        QueueTelemetry.CurrentCombinedRetainedBatchCount;

    public long CurrentCombinedRetainedPayloadBytes =>
        QueueTelemetry.CurrentCombinedRetainedPayloadBytes;

    public IReadOnlyList<string> Warnings => warnings;

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

