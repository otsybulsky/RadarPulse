using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedBatchResourceTests
{
    [Fact]
    public void ReleaseFailureIsTerminalAndDoesNotInvokeCallbackAgain()
    {
        var releaseCount = 0;
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: 2,
            release: () =>
            {
                releaseCount++;
                return RadarProcessingRetainedPayloadReleaseResult.Failed(
                    RadarProcessingRetainedPayloadStrategy.PooledCopy,
                    "pool return failed");
            });
        var retained = new RadarProcessingRetainedQueuedBatch(CreateQueuedBatch(), resource);

        var failed = retained.ReleasePending();
        var second = retained.ReleasePending();
        var cleanup = new RadarProcessingRetainedResourceCleanupResult([failed, second]);

        Assert.True(failed.IsFailure);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Failed, failed.Status);
        Assert.Equal("pool return failed", failed.Message);
        Assert.True(second.IsFailure);
        Assert.Equal(1, releaseCount);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.ReleaseFailed, resource.State);
        Assert.True(resource.IsTerminal);
        Assert.False(cleanup.IsSuccessful);
        Assert.Equal(2, cleanup.FailedCount);
    }

    [Fact]
    public void ReleaseCallbackExceptionIsTerminalCleanupFailure()
    {
        var releaseCount = 0;
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: 2,
            release: () =>
            {
                releaseCount++;
                throw new InvalidOperationException("pool return failed");
            });
        var retained = new RadarProcessingRetainedQueuedBatch(CreateQueuedBatch(), resource);

        var failed = retained.ReleasePending();
        var second = retained.ReleasePending();
        var cleanup = new RadarProcessingRetainedResourceCleanupResult([failed, second]);

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Failed, failed.Status);
        Assert.Contains("pool return failed", failed.Message, StringComparison.Ordinal);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Failed, second.Status);
        Assert.Equal(1, releaseCount);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.ReleaseFailed, resource.State);
        Assert.True(resource.IsTerminal);
        Assert.False(cleanup.IsSuccessful);
        Assert.Equal(2, cleanup.FailedCount);
    }
}
