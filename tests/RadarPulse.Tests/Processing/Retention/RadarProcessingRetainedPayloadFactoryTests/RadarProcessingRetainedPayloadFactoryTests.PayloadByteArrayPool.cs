using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    [Fact]
    public void RetainedPayloadByteArrayPoolReusesLargeReturnedArrays()
    {
        var fallback = new TrackingArrayPool<byte>();
        var pool = new RadarProcessingRetainedPayloadByteArrayPool(
            fallback,
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 32);

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
    public void RetainedPayloadByteArrayPoolKeepsSmallArraysOnFallbackPool()
    {
        var fallback = new TrackingArrayPool<byte>();
        var pool = new RadarProcessingRetainedPayloadByteArrayPool(
            fallback,
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 32);

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
    public void RetainedPayloadByteArrayPoolRoundsLargeRentCapacityForReuse()
    {
        var pool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 16);

        var first = pool.Rent(5);
        pool.Return(first);
        var second = pool.Rent(7);

        Assert.Equal(8, first.Length);
        Assert.Same(first, second);
    }

    [Fact]
    public void RetainedPayloadByteArrayPoolBoundsRetainedLargeArrays()
    {
        var pool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 1,
            maxRetainedBytes: 8);
        var first = pool.Rent(8);
        var second = pool.Rent(8);

        pool.Return(first);
        pool.Return(second);

        Assert.Equal(1, pool.RetainedArrayCount);
        Assert.Equal(8, pool.RetainedBytes);
    }

    [Fact]
    public void RetainedPayloadByteArrayPoolPrefersLargerArraysWithinBudget()
    {
        var pool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 8);
        var small = pool.Rent(4);
        var large = pool.Rent(8);

        pool.Return(small);
        pool.Return(large);
        var rented = pool.Rent(8);

        Assert.Same(large, rented);
        Assert.Equal(0, pool.RetainedArrayCount);
        Assert.Equal(0, pool.RetainedBytes);
    }

}
