namespace RadarPulse.Application.Product;

/// <summary>
/// Describes the input that produced a product run.
/// </summary>
public sealed record RadarPulseProductInputSummary(
    RadarPulseProductInputKind Kind,
    string Description,
    string Source,
    int BatchCount,
    long EventCount);

/// <summary>
/// One effective configuration value with provenance.
/// </summary>
public sealed record RadarPulseProductConfigurationValue(
    string Name,
    string Value,
    RadarPulseProductOptionSource Source);

/// <summary>
/// Resolved product pipeline configuration and validation posture for a run.
/// </summary>
public sealed record RadarPulseProductConfiguration(
    string ProfileName,
    bool IsValid,
    string? FirstInvalidOption,
    string? FirstInvalidReason,
    IReadOnlyList<RadarPulseProductConfigurationValue> Values,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Compact operator-facing run posture used by the UI, CLI, and control responses.
/// </summary>
/// <remarks>
/// This record is intentionally diagnosis-oriented: it exposes the first blocker,
/// handler posture, retained resource state, and fallback recommendation without
/// requiring callers to inspect durable envelope internals.
/// </remarks>
public sealed record RadarPulseProductOperatorSummary(
    RadarPulseProductRunState RunState,
    bool IsReady,
    bool ProcessingComplete,
    RadarPulseProductHandlerMode HandlerMode,
    bool HasHandlerConflict,
    string HandlerBlockingReason,
    string FirstBlockingReason,
    RadarPulseProductFallbackRecommendation FallbackRecommendation,
    string? FirstBlockingBatchId,
    long? FirstBlockingSequence,
    string? FirstBlockingState,
    long CurrentRetainedBatchCount,
    long CurrentRetainedPayloadBytes,
    bool ReleaseHealthy,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Local representative capacity and completeness evidence captured for a run.
/// </summary>
/// <remarks>
/// The values support local readiness and portfolio inspection. They are not a
/// production throughput certification or cross-machine benchmark claim.
/// </remarks>
public sealed record RadarPulseProductCapacityEvidence(
    string RunId,
    string ProfileName,
    double ElapsedMilliseconds,
    long MeasuredAllocatedBytes,
    long AcceptedBatchCount,
    long ProcessedBatchCount,
    long CommittedBatchCount,
    RadarPulseProductHandlerMode HandlerMode,
    string DurableAdapterKind,
    long TerminalRetainedBatchCount,
    long TerminalRetainedPayloadBytes,
    bool ProcessingCompletenessPassed,
    bool IsReady,
    string FirstBlockingReason,
    string ConfigurationContour);

/// <summary>
/// Diagnostic flags and retained-resource counters for a product run.
/// </summary>
public sealed record RadarPulseProductDiagnostics(
    bool ProcessingCompletenessPassed,
    bool IsReady,
    string BlockingReason,
    string HandlerOutputProvenance,
    bool UsesOrderedHandlerDeltaMerge,
    bool UsesSequentialHandlerFallback,
    bool HandlerOutputBlocked,
    long ReleaseFailureCount,
    long TerminalRetainedEnvelopeCount,
    long TerminalRetainedPayloadBytes,
    long CurrentRetainedBatchCount,
    long CurrentRetainedPayloadBytes,
    IReadOnlyList<string> Warnings);
