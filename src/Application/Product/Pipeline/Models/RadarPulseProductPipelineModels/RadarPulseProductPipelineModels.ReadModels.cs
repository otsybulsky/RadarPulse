namespace RadarPulse.Application.Product;

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
