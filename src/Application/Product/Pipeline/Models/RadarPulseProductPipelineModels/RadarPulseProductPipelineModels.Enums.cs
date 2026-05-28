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
