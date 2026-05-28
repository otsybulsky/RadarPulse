namespace RadarPulse.Application.Product;

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
