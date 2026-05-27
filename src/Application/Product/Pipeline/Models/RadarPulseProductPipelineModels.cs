namespace RadarPulse.Application.Product;

/// <summary>
/// Identifies the product-level input family that produced a pipeline run.
/// </summary>
public enum RadarPulseProductInputKind
{
    /// <summary>
    /// Deterministic in-memory batches generated for local demo and contract tests.
    /// </summary>
    Synthetic = 1,

    /// <summary>
    /// Batches projected from a local NEXRAD archive file.
    /// </summary>
    ArchiveFile = 2
}

/// <summary>
/// Product-facing lifecycle state for a run as exposed through CLI, HTTP, UI, and history.
/// </summary>
/// <remarks>
/// The state is intentionally coarser than the processing runtime internals. It gives
/// operators a stable product vocabulary without exposing queue, durable envelope, or
/// handler-specific implementation details directly.
/// </remarks>
public enum RadarPulseProductRunState
{
    /// <summary>
    /// The run has been described but no pipeline work has started.
    /// </summary>
    NotStarted = 1,

    /// <summary>
    /// The run is accepting or processing work.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The run has stopped accepting new work and is completing accepted work.
    /// </summary>
    Draining = 3,

    /// <summary>
    /// The run reached a successful terminal state.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// The run was stopped in a controlled terminal posture.
    /// </summary>
    Stopped = 5,

    /// <summary>
    /// The run cannot be considered ready until an exposed blocker is resolved.
    /// </summary>
    Blocked = 6,

    /// <summary>
    /// The run reached a failed terminal state.
    /// </summary>
    Failed = 7,

    /// <summary>
    /// Open work was canceled or released by an explicit control action.
    /// </summary>
    Canceled = 8
}

/// <summary>
/// Product-level classification for how custom handler output was produced.
/// </summary>
public enum RadarPulseProductHandlerMode
{
    /// <summary>
    /// The backend selected the accepted handler mode from the supplied handler set.
    /// </summary>
    Auto = 1,

    /// <summary>
    /// The run did not execute custom source handlers.
    /// </summary>
    HandlerFree = 2,

    /// <summary>
    /// Handler output was computed with mergeable per-batch deltas and ordered commit.
    /// </summary>
    MergeableDelta = 3,

    /// <summary>
    /// Stateful handler output was produced through the accepted sequential snapshot fallback.
    /// </summary>
    SnapshotSequential = 4
}

/// <summary>
/// Operator-facing next action suggested when a run is blocked or degraded.
/// </summary>
public enum RadarPulseProductFallbackRecommendation
{
    /// <summary>
    /// No operator fallback is currently recommended.
    /// </summary>
    None = 1,

    /// <summary>
    /// Correct invalid or incomplete product pipeline configuration.
    /// </summary>
    FixConfiguration = 2,

    /// <summary>
    /// Inspect durable adapter readiness, storage, or compatibility posture.
    /// </summary>
    InspectDurableAdapter = 3,

    /// <summary>
    /// Recover an envelope that was claimed but not completed or committed.
    /// </summary>
    RecoverClaimedEnvelope = 4,

    /// <summary>
    /// Retry or mark a failed durable envelope as poison according to local policy.
    /// </summary>
    RetryOrPoisonEnvelope = 5,

    /// <summary>
    /// Quarantine an envelope that cannot be safely retried.
    /// </summary>
    QuarantinePoisonEnvelope = 6,

    /// <summary>
    /// Release resources retained by canceled work.
    /// </summary>
    CleanupCanceledEnvelope = 7,

    /// <summary>
    /// Release retained resources that are no longer tied to active processing.
    /// </summary>
    ReleaseRetainedResources = 8,

    /// <summary>
    /// Complete, recover, or reject work that has not reached a commit decision.
    /// </summary>
    CompleteOrRecoverUncommittedWork = 9,

    /// <summary>
    /// Resolve a handler contract posture that blocks ready output.
    /// </summary>
    ResolveHandlerPosture = 10,

    /// <summary>
    /// Reject an unsafe fallback path rather than silently accepting degraded behavior.
    /// </summary>
    RejectUnsafeFallback = 11
}

/// <summary>
/// Records where an effective product pipeline option value came from.
/// </summary>
public enum RadarPulseProductOptionSource
{
    /// <summary>
    /// Built-in accepted product pipeline default.
    /// </summary>
    Default = 1,

    /// <summary>
    /// Named profile value selected by the production-shaped product pipeline.
    /// </summary>
    Profile = 2,

    /// <summary>
    /// Value explicitly supplied by a caller through product options.
    /// </summary>
    ExplicitOverride = 3,

    /// <summary>
    /// Value supplied by a focused test or deterministic harness.
    /// </summary>
    TestHarness = 4
}

/// <summary>
/// Supported handler sets for product demo and API-triggered pipeline runs.
/// </summary>
/// <remarks>
/// These values select representative handler contracts. They are not an open
/// plugin registry and intentionally stay inside the deterministic local product
/// demo boundary.
/// </remarks>
public enum RadarPulseProductHandlerSet
{
    /// <summary>
    /// Run without custom source handlers.
    /// </summary>
    None = 1,

    /// <summary>
    /// Lightweight mergeable counters and checksums.
    /// </summary>
    CounterChecksum = 2,

    /// <summary>
    /// Heavier mergeable counters and checksums used by performance gates.
    /// </summary>
    CounterChecksumHeavy = 3,

    /// <summary>
    /// Snapshot-style stateful counting handler that requires sequential fallback.
    /// </summary>
    SnapshotCounting = 4,

    /// <summary>
    /// Deliberately unsupported handler posture used to prove blocking behavior.
    /// </summary>
    Unsupported = 5
}

/// <summary>
/// Control action that can be applied to persisted or recoverable product pipeline work.
/// </summary>
public enum RadarPulseProductControlAction
{
    /// <summary>
    /// Stop accepting new durable work while leaving accepted work available for later action.
    /// </summary>
    StopAccepting = 1,

    /// <summary>
    /// Process accepted work until the recoverable queue is drained.
    /// </summary>
    DrainAccepted = 2,

    /// <summary>
    /// Cancel open work and release retained resources tied to canceled envelopes.
    /// </summary>
    CancelOpenAndRelease = 3,

    /// <summary>
    /// Reject a recovery path that would hide unsafe fallback behavior.
    /// </summary>
    RejectUnsafeFallback = 4
}

/// <summary>
/// Optional product-level overrides for the accepted production pipeline defaults.
/// </summary>
/// <remarks>
/// Null numeric values preserve the resolved product profile defaults. The
/// options are kept in product vocabulary even when they map to lower-level
/// processing queue, worker, retention, and ordered-concurrency settings.
/// </remarks>
public sealed record RadarPulseProductPipelineOptions(
    int? WorkerCount = null,
    int? WorkerQueueCapacity = null,
    int? ProviderQueueCapacity = null,
    long? RetainedPayloadBytes = null,
    int? OrderedActiveBatchCapacity = null,
    int? WorkloadBatchLimit = null,
    bool SilentBorrowedProviderFallback = false);

/// <summary>
/// Request for a deterministic synthetic product pipeline run.
/// </summary>
/// <remarks>
/// Synthetic runs are the primary local demo path. They generate archive-shaped
/// batches without network ingestion or external storage dependencies.
/// </remarks>
public sealed record RadarPulseProductPipelineSyntheticRunRequest(
    string RunId,
    int SourceCount = 2,
    int BatchCount = 2,
    int EventsPerBatch = 2,
    int PartitionCount = 0,
    int ShardCount = 0,
    RadarPulseProductHandlerSet HandlerSet = RadarPulseProductHandlerSet.None,
    RadarPulseProductPipelineOptions? Options = null);

/// <summary>
/// Request for a product pipeline run backed by one local NEXRAD archive file.
/// </summary>
/// <remarks>
/// The file is parsed into RadarEventBatch input before entering the accepted
/// production-shaped processing pipeline. This remains a local archive-shaped
/// path and does not claim true live radar ingestion.
/// </remarks>
public sealed record RadarPulseProductPipelineArchiveFileRunRequest(
    string RunId,
    string FilePath,
    int Parallelism = 1,
    int PartitionCount = 0,
    int ShardCount = 0,
    string Decompressor = "radarpulse",
    RadarPulseProductHandlerSet HandlerSet = RadarPulseProductHandlerSet.None,
    RadarPulseProductPipelineOptions? Options = null);

/// <summary>
/// Request body for product control actions over recoverable pipeline state.
/// </summary>
/// <remarks>
/// HTTP control endpoints set <see cref="Action"/> from the route so callers
/// cannot use one route name to execute a different control action.
/// </remarks>
public sealed record RadarPulseProductPipelineControlRequest(
    string RunId,
    RadarPulseProductControlAction Action,
    string DurableStorePath,
    int SourceCount = 2,
    int PartitionCount = 0,
    int ShardCount = 0,
    RadarPulseProductHandlerSet HandlerSet = RadarPulseProductHandlerSet.None,
    RadarPulseProductPipelineOptions? Options = null,
    string Message = "");

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

/// <summary>
/// Product-facing view of a processed or attempted provider batch.
/// </summary>
/// <remarks>
/// <see cref="ProviderSequence"/> is the stable ordering key used by the
/// accepted ordered commit path and by product API batch lookup.
/// </remarks>
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

/// <summary>
/// Dense source identity projected into product vocabulary.
/// </summary>
public sealed record RadarPulseProductSourceIdentity(
    int SourceId,
    int RadarOrdinal,
    int ElevationSlot,
    int AzimuthBucket,
    int RangeBand);

/// <summary>
/// One exported handler field value for a product source.
/// </summary>
public sealed record RadarPulseProductHandlerOutput(
    int HandlerIndex,
    string HandlerName,
    string Name,
    string Type,
    long Int64Value,
    double DoubleValue);

/// <summary>
/// Field descriptor for handler output exported through product run details.
/// </summary>
public sealed record RadarPulseProductHandlerField(
    int HandlerIndex,
    string HandlerName,
    string Name,
    string Type,
    int SlotIndex);

/// <summary>
/// Descriptor for one handler and its exported output fields.
/// </summary>
public sealed record RadarPulseProductHandlerDescriptor(
    int HandlerIndex,
    string Name,
    int Int64SlotCount,
    int DoubleSlotCount,
    string ExecutionClassification,
    IReadOnlyList<RadarPulseProductHandlerField> Fields);

/// <summary>
/// Product-level handler contract posture for a run.
/// </summary>
/// <remarks>
/// A blocked handler contract is surfaced as product readiness information rather
/// than hidden as an internal processing error.
/// </remarks>
public sealed record RadarPulseProductHandlerContract(
    string StatePosture,
    string Message,
    string? FirstBlockingReason,
    bool IsBlocked,
    IReadOnlyList<RadarPulseProductHandlerDescriptor> Handlers);

/// <summary>
/// Product-facing read model for one radar source after processing.
/// </summary>
public sealed record RadarPulseProductSource(
    RadarPulseProductSourceIdentity Identity,
    bool IsActive,
    long ProcessedEventCount,
    long ProcessedPayloadValueCount,
    long RawValueChecksum,
    long LastMessageTimestampUtcTicks,
    ulong ProcessingChecksum,
    IReadOnlyList<RadarPulseProductHandlerOutput> HandlerValues);

/// <summary>
/// Compact run listing shape persisted in history and returned by list endpoints.
/// </summary>
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

/// <summary>
/// Complete product run detail returned by run, lookup, and history workflows.
/// </summary>
/// <remarks>
/// This is the stable aggregate consumed by the operator UI. It keeps summary,
/// configuration, operator posture, capacity evidence, diagnostics, handler
/// contract, batches, and sources together so persisted history can be reloaded
/// without recomputing the backend run.
/// </remarks>
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
    /// <summary>
    /// Convenience alias for <see cref="RadarPulseProductRunSummary.RunId"/>.
    /// </summary>
    public string RunId => Summary.RunId;

    /// <summary>
    /// Convenience alias for <see cref="RadarPulseProductRunSummary.IsReady"/>.
    /// </summary>
    public bool IsReady => Summary.IsReady;

    /// <summary>
    /// Convenience alias for <see cref="RadarPulseProductRunSummary.HasReadModel"/>.
    /// </summary>
    public bool HasReadModel => Summary.HasReadModel;
}

/// <summary>
/// Internal product query result that distinguishes missing data from successful lookup.
/// </summary>
/// <remarks>
/// Service methods use this shape before the HTTP-facing API response maps it
/// to status codes.
/// </remarks>
public sealed record RadarPulseProductQueryResult<T>(
    bool Found,
    T? Value,
    string Message)
{
    /// <summary>
    /// Creates a found result around a non-null value.
    /// </summary>
    public static RadarPulseProductQueryResult<T> FromValue(T value) =>
        new(true, value, string.Empty);

    /// <summary>
    /// Creates a not-found result with a caller-facing explanation.
    /// </summary>
    public static RadarPulseProductQueryResult<T> NotFound(string message) =>
        new(false, default, message);
}

/// <summary>
/// Product API response envelope shared by CLI and HTTP adapter surfaces.
/// </summary>
/// <remarks>
/// The HTTP adapter uses <see cref="StatusCode"/> directly, while in-process
/// callers can inspect <see cref="IsSuccess"/> and <see cref="Message"/>.
/// </remarks>
public sealed record RadarPulseProductApiResponse<T>(
    int StatusCode,
    bool IsSuccess,
    T? Body,
    string Message)
{
    /// <summary>
    /// Creates a successful response for an existing resource or query result.
    /// </summary>
    public static RadarPulseProductApiResponse<T> Ok(T body) =>
        new(200, true, body, string.Empty);

    /// <summary>
    /// Creates a successful response for a newly created run or control result.
    /// </summary>
    public static RadarPulseProductApiResponse<T> Created(T body) =>
        new(201, true, body, string.Empty);

    /// <summary>
    /// Creates a client error response for invalid product input.
    /// </summary>
    public static RadarPulseProductApiResponse<T> BadRequest(string message) =>
        new(400, false, default, message);

    /// <summary>
    /// Creates a missing-resource response for product lookup routes.
    /// </summary>
    public static RadarPulseProductApiResponse<T> NotFound(string message) =>
        new(404, false, default, message);
}

/// <summary>
/// Result of applying a product control action.
/// </summary>
public sealed record RadarPulseProductControlSummary(
    string RunId,
    string Action,
    RadarPulseProductOperatorSummary OperatorSummary,
    int CanceledOpenCount,
    int ReleasedCanceledCount,
    int DrainedProcessingCount,
    string Message);
