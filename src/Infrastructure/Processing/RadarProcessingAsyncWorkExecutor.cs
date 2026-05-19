using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public delegate ValueTask<RadarProcessingAsyncWorkCompletion> RadarProcessingAsyncWorkExecutor(
    RadarProcessingAsyncWorkItem workItem,
    CancellationToken cancellationToken);
