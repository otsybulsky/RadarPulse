using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Executes one routed shard work item for a batch dispatch.
/// </summary>
public delegate ValueTask<RadarProcessingAsyncWorkCompletion> RadarProcessingAsyncDispatchExecutor(
    RadarEventBatch batch,
    RadarProcessingBatchRoute route,
    RadarProcessingAsyncWorkItem workItem,
    CancellationToken cancellationToken);
