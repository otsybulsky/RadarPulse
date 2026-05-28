using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    [Fact]
    public void SnapshotCopyRetainsLeasedBatchWithCurrentOwnedSnapshotBehavior()
    {
        var factory = new RadarProcessingRetainedPayloadFactory();
        var builder = CreateBuilder([1, 2, 3]);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(batch);
            Assert.True(result.IsSuccessful);
            Assert.NotSame(batch, result.Batch);
            Assert.Equal(RadarEventBatchLifetime.Owned, result.Batch!.Lifetime);
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.Strategy);
            Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, result.Resource!.Release().Status);
        });

        AddEvent(builder, [9]);
        var reused = builder.Build();

        Assert.NotNull(result);
        Assert.Equal([1, 2, 3], result.Batch!.Payload.ToArray());
        Assert.Equal([9], reused.Payload.ToArray());
        Assert.Equal(1, result.EventCount);
        Assert.Equal(3, result.PayloadBytes);
        Assert.Equal(3, result.PayloadValueCount);
        Assert.Equal(6, result.RawValueChecksum);
        Assert.True(result.Elapsed >= TimeSpan.Zero);
        Assert.True(result.AllocatedBytes >= 0);
    }

    [Fact]
    public void SnapshotCopyReturnsOwnedInputWithoutExtraRetainedResource()
    {
        var factory = new RadarProcessingRetainedPayloadFactory();
        var owned = CreateBuilder([7, 8]).Build();

        var result = factory.Retain(owned);

        Assert.True(result.IsSuccessful);
        Assert.Same(owned, result.Batch);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.Resource!.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, result.Resource.Release().Status);
    }

}
