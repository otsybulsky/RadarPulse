namespace RadarPulse.Application.Product;

public enum RadarPulseProductInputKind
{
    Synthetic = 1,
    ArchiveFile = 2
}

public enum RadarPulseProductRunState
{
    NotStarted = 1,
    Running = 2,
    Draining = 3,
    Completed = 4,
    Stopped = 5,
    Blocked = 6,
    Failed = 7,
    Canceled = 8
}

public enum RadarPulseProductHandlerMode
{
    Auto = 1,
    HandlerFree = 2,
    MergeableDelta = 3,
    SnapshotSequential = 4
}

public enum RadarPulseProductFallbackRecommendation
{
    None = 1,
    FixConfiguration = 2,
    InspectDurableAdapter = 3,
    RecoverClaimedEnvelope = 4,
    RetryOrPoisonEnvelope = 5,
    QuarantinePoisonEnvelope = 6,
    CleanupCanceledEnvelope = 7,
    ReleaseRetainedResources = 8,
    CompleteOrRecoverUncommittedWork = 9,
    ResolveHandlerPosture = 10,
    RejectUnsafeFallback = 11
}

public enum RadarPulseProductOptionSource
{
    Default = 1,
    Profile = 2,
    ExplicitOverride = 3,
    TestHarness = 4
}

public enum RadarPulseProductHandlerSet
{
    None = 1,
    CounterChecksum = 2,
    CounterChecksumHeavy = 3,
    SnapshotCounting = 4,
    Unsupported = 5
}

public sealed record RadarPulseProductPipelineOptions(
    int? WorkerCount = null,
    int? WorkerQueueCapacity = null,
    int? ProviderQueueCapacity = null,
    long? RetainedPayloadBytes = null,
    int? OrderedActiveBatchCapacity = null,
    int? WorkloadBatchLimit = null,
    bool SilentBorrowedProviderFallback = false);

public sealed record RadarPulseProductPipelineSyntheticRunRequest(
    string RunId,
    int SourceCount = 2,
    int BatchCount = 2,
    int EventsPerBatch = 2,
    int PartitionCount = 0,
    int ShardCount = 0,
    RadarPulseProductHandlerSet HandlerSet = RadarPulseProductHandlerSet.None,
    RadarPulseProductPipelineOptions? Options = null);

public sealed record RadarPulseProductPipelineArchiveFileRunRequest(
    string RunId,
    string FilePath,
    int Parallelism = 1,
    int PartitionCount = 0,
    int ShardCount = 0,
    string Decompressor = "radarpulse",
    RadarPulseProductHandlerSet HandlerSet = RadarPulseProductHandlerSet.None,
    RadarPulseProductPipelineOptions? Options = null);

public sealed record RadarPulseProductInputSummary(
    RadarPulseProductInputKind Kind,
    string Description,
    string Source,
    int BatchCount,
    long EventCount);

public sealed record RadarPulseProductConfigurationValue(
    string Name,
    string Value,
    RadarPulseProductOptionSource Source);

public sealed record RadarPulseProductConfiguration(
    string ProfileName,
    bool IsValid,
    string? FirstInvalidOption,
    string? FirstInvalidReason,
    IReadOnlyList<RadarPulseProductConfigurationValue> Values,
    IReadOnlyList<string> Warnings);

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

public sealed record RadarPulseProductBatch(
    long ProviderSequence,
    bool WasAccepted,
    int StreamEventCount,
    int PayloadBytes,
    long PayloadValueCount,
    long RawValueChecksum,
    string? ProcessingStatus,
    bool IsSuccessful,
    string Message,
    long? TopologyVersion);

public sealed record RadarPulseProductSourceIdentity(
    int SourceId,
    int RadarOrdinal,
    int ElevationSlot,
    int AzimuthBucket,
    int RangeBand);

public sealed record RadarPulseProductHandlerOutput(
    int HandlerIndex,
    string HandlerName,
    string Name,
    string Type,
    long Int64Value,
    double DoubleValue);

public sealed record RadarPulseProductHandlerField(
    int HandlerIndex,
    string HandlerName,
    string Name,
    string Type,
    int SlotIndex);

public sealed record RadarPulseProductHandlerDescriptor(
    int HandlerIndex,
    string Name,
    int Int64SlotCount,
    int DoubleSlotCount,
    string ExecutionClassification,
    IReadOnlyList<RadarPulseProductHandlerField> Fields);

public sealed record RadarPulseProductHandlerContract(
    string StatePosture,
    string Message,
    string? FirstBlockingReason,
    bool IsBlocked,
    IReadOnlyList<RadarPulseProductHandlerDescriptor> Handlers);

public sealed record RadarPulseProductSource(
    RadarPulseProductSourceIdentity Identity,
    bool IsActive,
    long ProcessedEventCount,
    long ProcessedPayloadValueCount,
    long RawValueChecksum,
    long LastMessageTimestampUtcTicks,
    ulong ProcessingChecksum,
    IReadOnlyList<RadarPulseProductHandlerOutput> HandlerValues);

public sealed record RadarPulseProductRunSummary(
    string RunId,
    RadarPulseProductInputSummary Input,
    RadarPulseProductRunState State,
    bool IsReady,
    bool HasReadModel,
    RadarPulseProductHandlerMode HandlerMode,
    string FirstBlockingReason,
    RadarPulseProductFallbackRecommendation FallbackRecommendation,
    int BatchCount,
    int SourceCount,
    long AcceptedBatchCount,
    long ProcessedBatchCount,
    long CommittedBatchCount,
    int WarningCount);

public sealed record RadarPulseProductRunDetail(
    RadarPulseProductRunSummary Summary,
    RadarPulseProductConfiguration Configuration,
    RadarPulseProductOperatorSummary OperatorSummary,
    RadarPulseProductCapacityEvidence CapacityEvidence,
    RadarPulseProductDiagnostics? Diagnostics,
    RadarPulseProductHandlerContract? HandlerContract,
    IReadOnlyList<RadarPulseProductBatch> Batches,
    IReadOnlyList<RadarPulseProductSource> Sources,
    string Message)
{
    public string RunId => Summary.RunId;

    public bool IsReady => Summary.IsReady;

    public bool HasReadModel => Summary.HasReadModel;
}

public sealed record RadarPulseProductQueryResult<T>(
    bool Found,
    T? Value,
    string Message)
{
    public static RadarPulseProductQueryResult<T> FromValue(T value) =>
        new(true, value, string.Empty);

    public static RadarPulseProductQueryResult<T> NotFound(string message) =>
        new(false, default, message);
}

public sealed record RadarPulseProductControlSummary(
    string RunId,
    string Action,
    RadarPulseProductOperatorSummary OperatorSummary,
    int CanceledOpenCount,
    int ReleasedCanceledCount,
    int DrainedProcessingCount,
    string Message);
