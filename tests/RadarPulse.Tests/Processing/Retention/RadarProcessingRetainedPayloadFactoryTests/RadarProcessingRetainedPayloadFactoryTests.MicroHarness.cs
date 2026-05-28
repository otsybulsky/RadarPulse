using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    [Fact]
    public void PooledCopyMicroHarnessSeparatesColdAndWarmLargePayloadRents()
    {
        const int payloadLength = 256 * 1024;
        var eventPool = new TrackingArrayPool<RadarStreamEvent>();
        var payloadPool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 128 * 1024,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 512 * 1024);
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);

        var warmup = RetainLeasedPooledCopy(factory, payloadLength: 1);
        warmup.Resource!.Release();

        var cold = RetainLeasedPooledCopy(factory, payloadLength);
        var coldRelease = cold.Resource!.Release();
        var warm = RetainLeasedPooledCopy(factory, payloadLength);
        var warmRelease = warm.Resource!.Release();

        Assert.Equal(2, cold.PoolRentCount);
        Assert.Equal(1, cold.PoolMissCount);
        Assert.Equal(1, cold.EventPoolRentCount);
        Assert.Equal(1, cold.PayloadPoolRentCount);
        Assert.Equal(0, cold.EventPoolMissCount);
        Assert.Equal(1, cold.PayloadPoolMissCount);
        Assert.Equal(2, coldRelease.PoolReturnCount);
        Assert.Equal(2, warm.PoolRentCount);
        Assert.Equal(0, warm.PoolMissCount);
        Assert.Equal(1, warm.EventPoolRentCount);
        Assert.Equal(1, warm.PayloadPoolRentCount);
        Assert.Equal(0, warm.EventPoolMissCount);
        Assert.Equal(0, warm.PayloadPoolMissCount);
        Assert.Equal(2, warmRelease.PoolReturnCount);
        Assert.True(cold.AllocatedBytes > warm.AllocatedBytes);
        Assert.Equal(1, payloadPool.MissCount);
        Assert.Equal(1, payloadPool.RetainedArrayCount);
        Assert.Equal(payloadLength, payloadPool.RetainedBytes);
    }

    [Fact]
    public void PooledCopyMicroHarnessSeparatesColdAndWarmLargeEventRents()
    {
        const int eventCount = 4096;
        var eventPool = new RadarProcessingRetainedEventArrayPool(
            largeArrayThreshold: 1024,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 512 * 1024);
        var payloadPool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 1024 * 1024,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 2 * 1024 * 1024);
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);

        var warmup = RetainLeasedPooledCopyWithEventCount(factory, eventCount: 1);
        warmup.Resource!.Release();

        var cold = RetainLeasedPooledCopyWithEventCount(factory, eventCount);
        var coldRelease = cold.Resource!.Release();
        var warm = RetainLeasedPooledCopyWithEventCount(factory, eventCount);
        var warmRelease = warm.Resource!.Release();

        Assert.Equal(2, cold.PoolRentCount);
        Assert.Equal(1, cold.PoolMissCount);
        Assert.Equal(1, cold.EventPoolRentCount);
        Assert.Equal(1, cold.PayloadPoolRentCount);
        Assert.Equal(1, cold.EventPoolMissCount);
        Assert.Equal(0, cold.PayloadPoolMissCount);
        Assert.Equal(2, coldRelease.PoolReturnCount);
        Assert.Equal(1, coldRelease.EventPoolReturnCount);
        Assert.Equal(1, coldRelease.PayloadPoolReturnCount);
        Assert.Equal(2, warm.PoolRentCount);
        Assert.Equal(0, warm.PoolMissCount);
        Assert.Equal(1, warm.EventPoolRentCount);
        Assert.Equal(1, warm.PayloadPoolRentCount);
        Assert.Equal(0, warm.EventPoolMissCount);
        Assert.Equal(0, warm.PayloadPoolMissCount);
        Assert.Equal(2, warmRelease.PoolReturnCount);
        Assert.Equal(1, eventPool.MissCount);
        Assert.Equal(1, eventPool.RetainedArrayCount);
        Assert.Equal(eventCount, eventPool.RetainedEventCount);
        Assert.Equal(eventCount * RadarStreamEvent.SizeInBytes, eventPool.RetainedBytes);
    }

    [Fact]
    public void PooledCopyPrewarmMovesLargeArrayAllocationBeforeRetention()
    {
        const int eventCount = 8;
        const int payloadLength = 8;
        var eventPool = new RadarProcessingRetainedEventArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 64 * RadarStreamEvent.SizeInBytes);
        var payloadPool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 64);
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);

        var prewarm = factory.Prewarm(eventCount, payloadLength);
        var retained = RetainLeasedPooledCopyWithEventCount(factory, eventCount);

        Assert.True(prewarm.AllocatedBytes > 0);
        Assert.Equal(eventCount, prewarm.EventCount);
        Assert.Equal(payloadLength, prewarm.PayloadBytes);
        Assert.Equal(1, prewarm.RetainedBatchCount);
        Assert.Equal(eventCount * RadarStreamEvent.SizeInBytes, prewarm.EventPoolRetainedBytes);
        Assert.Equal(payloadLength, prewarm.PayloadPoolRetainedBytes);
        Assert.Equal(2, retained.PoolRentCount);
        Assert.Equal(0, retained.PoolMissCount);
        Assert.Equal(0, retained.EventPoolMissCount);
        Assert.Equal(0, retained.PayloadPoolMissCount);

        var release = retained.Resource!.Release();

        Assert.Equal(2, release.PoolReturnCount);
        Assert.Equal(0, eventPool.MissCount);
        Assert.Equal(0, payloadPool.MissCount);
        Assert.Equal(1, eventPool.RetainedArrayCount);
        Assert.Equal(1, payloadPool.RetainedArrayCount);
    }

}
