using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    [Fact]
    public void RetainedEventArrayPoolReusesLargeReturnedArrays()
    {
        var fallback = new TrackingArrayPool<RadarStreamEvent>();
        var pool = new RadarProcessingRetainedEventArrayPool(
            fallback,
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 32 * RadarStreamEvent.SizeInBytes);

        var first = pool.Rent(8);
        pool.Return(first);
        var second = pool.Rent(7);

        Assert.Same(first, second);
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.Equal(1, pool.MissCount);
        Assert.Equal(0, fallback.RentCount);
        Assert.Equal(0, fallback.ReturnCount);
        Assert.Equal(0, pool.RetainedArrayCount);
        Assert.Equal(0, pool.RetainedBytes);
    }

    [Fact]
    public void RetainedEventArrayPoolKeepsSmallArraysOnFallbackPool()
    {
        var fallback = new TrackingArrayPool<RadarStreamEvent>();
        var pool = new RadarProcessingRetainedEventArrayPool(
            fallback,
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 32 * RadarStreamEvent.SizeInBytes);

        var small = pool.Rent(3);
        pool.Return(small);

        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.Equal(0, pool.MissCount);
        Assert.Equal(1, fallback.RentCount);
        Assert.Equal(1, fallback.ReturnCount);
        Assert.Equal(0, pool.RetainedArrayCount);
        Assert.Equal(0, pool.RetainedBytes);
    }

    [Fact]
    public void RetainedEventArrayPoolBoundsRetainedLargeArraysByBytes()
    {
        var pool = new RadarProcessingRetainedEventArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 1,
            maxRetainedBytes: 8 * RadarStreamEvent.SizeInBytes);
        var first = pool.Rent(8);
        var second = pool.Rent(8);

        pool.Return(first);
        pool.Return(second);

        Assert.Equal(1, pool.RetainedArrayCount);
        Assert.Equal(8, pool.RetainedEventCount);
        Assert.Equal(8 * RadarStreamEvent.SizeInBytes, pool.RetainedBytes);
    }

}
