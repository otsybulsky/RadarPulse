using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Executes one async shard work item and returns its measured completion contract.
/// </summary>
public delegate ValueTask<RadarProcessingAsyncWorkCompletion> RadarProcessingAsyncWorkExecutor(
    RadarProcessingAsyncWorkItem workItem,
    CancellationToken cancellationToken);
