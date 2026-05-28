using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    [Fact]
    public void PooledCopyRetainsLeasedBatchAndReturnsRentedStorageOnRelease()
    {
        var eventPool = new TrackingArrayPool<RadarStreamEvent>();
        var payloadPool = new TrackingArrayPool<byte>();
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);
        var builder = CreateBuilder([1, 2, 3]);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
            Assert.True(result.IsSuccessful);
            Assert.NotSame(batch, result.Batch);
            Assert.Equal(RadarEventBatchLifetime.Owned, result.Batch!.Lifetime);
            Assert.Equal([1, 2, 3], result.Batch.Payload.ToArray());
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.Resource!.Strategy);
            Assert.Equal(RadarProcessingRetainedBatchResourceState.ProviderOwned, result.Resource.State);
            Assert.Equal(1, eventPool.RentCount);
            Assert.Equal(1, payloadPool.RentCount);
            Assert.Equal(2, result.PoolRentCount);
            Assert.Equal(0, result.PoolMissCount);
            Assert.Equal(1, result.EventPoolRentCount);
            Assert.Equal(1, result.PayloadPoolRentCount);
            Assert.Equal(0, result.EventPoolMissCount);
            Assert.Equal(0, result.PayloadPoolMissCount);
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

        var release = result.Resource!.Release();
        var secondRelease = result.Resource.Release();

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, release.Status);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased, secondRelease.Status);
        Assert.Equal(1, eventPool.ReturnCount);
        Assert.Equal(1, payloadPool.ReturnCount);
        Assert.Equal(3, release.PayloadBytes);
        Assert.Equal(2, release.PoolReturnCount);
        Assert.Equal(1, release.EventPoolReturnCount);
        Assert.Equal(1, release.PayloadPoolReturnCount);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, result.Resource.State);
    }

    [Fact]
    public void PooledCopyKeepsOwnedInputAndDoesNotRentStorage()
    {
        var eventPool = new TrackingArrayPool<RadarStreamEvent>();
        var payloadPool = new TrackingArrayPool<byte>();
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);
        var owned = CreateBuilder([7, 8]).Build();

        var result = factory.Retain(
            owned,
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));

        Assert.True(result.IsSuccessful);
        Assert.Same(owned, result.Batch);
        Assert.Equal(0, eventPool.RentCount);
        Assert.Equal(0, payloadPool.RentCount);
        Assert.Equal(0, result.PoolRentCount);
        Assert.Equal(0, result.PoolMissCount);
        Assert.Equal(0, result.EventPoolRentCount);
        Assert.Equal(0, result.PayloadPoolRentCount);
        Assert.Equal(0, result.EventPoolMissCount);
        Assert.Equal(0, result.PayloadPoolMissCount);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, result.Resource!.Release().Status);
    }

    [Fact]
    public void EmptyPooledCopyDoesNotRentStorage()
    {
        var eventPool = new TrackingArrayPool<RadarStreamEvent>();
        var payloadPool = new TrackingArrayPool<byte>();
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 0, initialPayloadCapacity: 0);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        });

        Assert.NotNull(result);
        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Batch!.EventCount);
        Assert.Equal(0, result.Batch.PayloadLength);
        Assert.Equal(0, eventPool.RentCount);
        Assert.Equal(0, payloadPool.RentCount);
        Assert.Equal(0, result.PoolRentCount);
        Assert.Equal(0, result.PoolMissCount);
        Assert.Equal(0, result.EventPoolRentCount);
        Assert.Equal(0, result.PayloadPoolRentCount);
        Assert.Equal(0, result.EventPoolMissCount);
        Assert.Equal(0, result.PayloadPoolMissCount);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, result.Resource!.Release().Status);
    }

}
