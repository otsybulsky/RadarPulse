using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRetainedPayloadFactoryTests
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
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, result.Resource!.Release().Status);
    }

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
        Assert.Equal(2, coldRelease.PoolReturnCount);
        Assert.Equal(2, warm.PoolRentCount);
        Assert.Equal(0, warm.PoolMissCount);
        Assert.Equal(2, warmRelease.PoolReturnCount);
        Assert.True(cold.AllocatedBytes > warm.AllocatedBytes);
        Assert.Equal(1, payloadPool.MissCount);
        Assert.Equal(1, payloadPool.RetainedArrayCount);
        Assert.Equal(payloadLength, payloadPool.RetainedBytes);
    }

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

    [Fact]
    public void BuilderTransferIsExplicitlyUnsupportedUntilTransferSemanticsAreProven()
    {
        var factory = new RadarProcessingRetainedPayloadFactory();
        var batch = CreateBuilder([1]).Build();

        var result = factory.Retain(
            batch,
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.BuilderTransfer));

        Assert.False(result.IsSuccessful);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy, result.Status);
        Assert.Null(result.Batch);
        Assert.Null(result.Resource);
        Assert.Contains("not implemented", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetentionCanBeCanceledBeforeCopyStarts()
    {
        var factory = new RadarProcessingRetainedPayloadFactory();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = factory.Retain(
            CreateBuilder([1]).Build(),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy),
            cancellation.Token);

        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.Canceled, result.Status);
        Assert.False(result.IsSuccessful);
        Assert.Null(result.Batch);
        Assert.Null(result.Resource);
    }

    [Fact]
    public void PooledCopyFailureReturnsRentedStorageBeforeReportingFailure()
    {
        var eventPool = new TrackingArrayPool<RadarStreamEvent>();
        var payloadPool = new ThrowingArrayPool<byte>();
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);
        var builder = CreateBuilder([1, 2]);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        });

        Assert.NotNull(result);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.FailedCopy, result.Status);
        Assert.Null(result.Batch);
        Assert.Null(result.Resource);
        Assert.Equal(1, eventPool.RentCount);
        Assert.Equal(1, eventPool.ReturnCount);
    }

    private static RadarEventBatchBuilder CreateBuilder(
        byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(builder, payload);
        return builder;
    }

    private static RadarProcessingRetainedPayloadRetentionResult RetainLeasedPooledCopy(
        RadarProcessingRetainedPayloadFactory factory,
        int payloadLength)
    {
        var builder = CreateBuilderWithPayloadLength(payloadLength);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        });

        Assert.NotNull(result);
        Assert.True(result.IsSuccessful);
        return result;
    }

    private static RadarEventBatchBuilder CreateBuilderWithPayloadLength(
        int payloadLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadLength);

        var eventCount = payloadLength == 0
            ? 0
            : ((payloadLength - 1) / ushort.MaxValue) + 1;
        var builder = new RadarEventBatchBuilder(eventCount, payloadLength);
        var remaining = payloadLength;
        var eventIndex = 0;
        while (remaining > 0)
        {
            var chunkLength = Math.Min(remaining, ushort.MaxValue);
            var payload = new byte[chunkLength];
            payload[0] = (byte)((eventIndex % 251) + 1);

            AddEvent(builder, payload);
            remaining -= chunkLength;
            eventIndex++;
        }

        return builder;
    }

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        byte[] payload)
    {
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }

    private sealed class TrackingArrayPool<T> : ArrayPool<T>
    {
        public int RentCount { get; private set; }

        public int ReturnCount { get; private set; }

        public override T[] Rent(int minimumLength)
        {
            RentCount++;
            return new T[minimumLength];
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            ArgumentNullException.ThrowIfNull(array);
            ReturnCount++;
            if (clearArray)
            {
                Array.Clear(array);
            }
        }
    }

    private sealed class ThrowingArrayPool<T> : ArrayPool<T>
    {
        public override T[] Rent(int minimumLength) =>
            throw new InvalidOperationException("rent failed");

        public override void Return(T[] array, bool clearArray = false)
        {
        }
    }
}
