using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed partial record ProcessingBenchmarkArchiveRebalanceOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> Modes,
    int PartitionCount,
    int ShardCount,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    RadarProcessingValidationProfile ValidationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides QuarantineLifecycleOverrides,
    RadarProcessingTelemetryRetentionOptions TelemetryRetention,
    RadarProcessingPressureSkewOptions PressureSkew,
    RadarProcessingArchiveProviderMode ProviderMode = RadarProcessingArchiveProviderMode.BlockingBorrowed,
    int ProviderQueueCapacity = 1,
    TimeSpan? ProviderQueueTimeout = null,
    RadarProcessingQueuedProviderOverlapMode ProviderOverlapMode = RadarProcessingQueuedProviderOverlapMode.None,
    RadarProcessingRetainedPayloadStrategy RetentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
    long? ProviderQueueRetainedPayloadBytes = null,
    TimeSpan OverlapConsumerDelay = default,
    ProcessingBenchmarkProviderQueueTelemetryOutput QueueTelemetryOutput =
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
    ProcessingBenchmarkProviderOverlapTelemetryOutput OverlapTelemetryOutput =
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingAsyncExecutionOptions? AsyncExecution = null,
    ProcessingBenchmarkArchiveRebalanceOptionProvenance? OptionProvenance = null)
{
    public const int DefaultCandidateProviderQueueCapacity =
        RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity;
    public const long DefaultCandidateRetainedPayloadBytes =
        RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes;
    public const int DefaultRolloutWorkerCount = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount;
    public const int DefaultRolloutProviderQueueCapacity = DefaultCandidateProviderQueueCapacity;
    public const long DefaultRolloutRetainedPayloadBytes = DefaultCandidateRetainedPayloadBytes;
    public const string NaturalDefaultCandidateEvidenceContour = "natural-default-candidate";
    public const string ControlledProofEvidenceContour = "controlled-proof";
    public const string NaturalOptInEvidenceContour = "natural-opt-in";
    public const string NotApplicableEvidenceContour = "not-applicable";
    public const string NaturalReadinessEvidenceScope = "natural-readiness";
    public const string ControlledMechanicsEvidenceScope = "controlled-mechanics-proof";
    public const string OptInDiagnosticEvidenceScope = "opt-in-diagnostic";
    public const string NotApplicableEvidenceScope = "not-applicable";

    /// <summary>
    /// Gets whether options match the rollout default provider-overlap evidence contour.
    /// </summary>
    public bool IsDefaultCandidateContour =>
        MatchesDefaultCandidateContour(
            ProviderMode,
            ProviderQueueCapacity,
            ProviderOverlapMode,
            RetentionStrategy,
            ProviderQueueRetainedPayloadBytes,
            OverlapConsumerDelay,
            QueueTelemetryOutput,
            OverlapTelemetryOutput,
            ExecutionMode);

    /// <summary>
    /// Gets whether the run is a controlled producer/consumer overlap proof.
    /// </summary>
    public bool IsControlledProviderOverlapProof =>
        ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
        ProviderOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer &&
        OverlapConsumerDelay > TimeSpan.Zero;

    /// <summary>
    /// Gets the provider-overlap evidence contour label for reporting.
    /// </summary>
    public string ProviderOverlapEvidenceContour =>
        FormatProviderOverlapEvidenceContour(
            ProviderMode,
            ProviderOverlapMode,
            OverlapConsumerDelay,
            IsDefaultCandidateContour);

    /// <summary>
    /// Gets the provider-overlap evidence scope label for reporting.
    /// </summary>
    public string ProviderOverlapEvidenceScope =>
        FormatProviderOverlapEvidenceScope(ProviderOverlapEvidenceContour);

    /// <summary>
    /// Gets explicit or current-default provenance for option values.
    /// </summary>
    public ProcessingBenchmarkArchiveRebalanceOptionProvenance EffectiveOptionProvenance =>
        OptionProvenance ?? ProcessingBenchmarkArchiveRebalanceOptionProvenance.CurrentDefaults;

    /// <summary>
    /// Gets whether the provider mode was explicitly set back to blocking borrowed.
    /// </summary>
    public bool IsExplicitBlockingBorrowedFallback =>
        ProviderMode == RadarProcessingArchiveProviderMode.BlockingBorrowed &&
        EffectiveOptionProvenance.ProviderMode == ProcessingBenchmarkOptionValueSource.Explicit;

    /// <summary>
    /// Gets whether rollout defaults expanded into the default evidence contour.
    /// </summary>
    public bool IsRolloutDefaultExpandedContour =>
        IsDefaultCandidateContour &&
        EffectiveOptionProvenance.ProviderMode == ProcessingBenchmarkOptionValueSource.RolloutDefault;

    /// <summary>
    /// Checks whether supplied provider and execution options match the rollout default evidence contour.
    /// </summary>
}
