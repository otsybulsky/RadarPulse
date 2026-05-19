using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public delegate ValueTask<RadarProcessingAsyncWorkCompletion> RadarProcessingAsyncDispatchExecutor(
    RadarEventBatch batch,
    RadarProcessingBatchRoute route,
    RadarProcessingAsyncWorkItem workItem,
    CancellationToken cancellationToken);
