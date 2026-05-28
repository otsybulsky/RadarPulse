using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed partial record ProcessingBenchmarkArchiveRebalanceOptions
{
    public static bool MatchesDefaultCandidateContour(
        RadarProcessingArchiveProviderMode providerMode,
        int providerQueueCapacity,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? providerQueueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput,
        RadarProcessingExecutionMode executionMode) =>
        providerMode == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode &&
        providerQueueCapacity == DefaultCandidateProviderQueueCapacity &&
        providerOverlapMode == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode &&
        retentionStrategy == RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy &&
        providerQueueRetainedPayloadBytes == DefaultCandidateRetainedPayloadBytes &&
        overlapConsumerDelay == RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay &&
        queueTelemetryOutput != ProcessingBenchmarkProviderQueueTelemetryOutput.None &&
        overlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None &&
        executionMode == RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;

    /// <summary>
    /// Formats the provider-overlap evidence contour from provider and overlap settings.
    /// </summary>
    public static string FormatProviderOverlapEvidenceContour(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        TimeSpan overlapConsumerDelay,
        bool isDefaultCandidateContour)
    {
        if (overlapConsumerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapConsumerDelay));
        }

        if (isDefaultCandidateContour)
        {
            return NaturalDefaultCandidateEvidenceContour;
        }

        return providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? overlapConsumerDelay > TimeSpan.Zero
                ? ControlledProofEvidenceContour
                : NaturalOptInEvidenceContour
            : NotApplicableEvidenceContour;
    }

    /// <summary>
    /// Formats the evidence scope label for a provider-overlap contour.
    /// </summary>
    public static string FormatProviderOverlapEvidenceScope(string providerOverlapEvidenceContour)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOverlapEvidenceContour);

        return providerOverlapEvidenceContour switch
        {
            NaturalDefaultCandidateEvidenceContour => NaturalReadinessEvidenceScope,
            ControlledProofEvidenceContour => ControlledMechanicsEvidenceScope,
            NaturalOptInEvidenceContour => OptInDiagnosticEvidenceScope,
            NotApplicableEvidenceContour => NotApplicableEvidenceScope,
            _ => throw new ArgumentException(
                "Unknown provider overlap evidence contour.",
                nameof(providerOverlapEvidenceContour))
        };
    }

    /// <summary>
    /// Parses archive rebalance benchmark options from CLI arguments.
    /// </summary>
}
