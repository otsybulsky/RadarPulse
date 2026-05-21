using System.Buffers;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingRetainedPayloadByteArrayPool : ArrayPool<byte>
{
    public const int DefaultLargeArrayThreshold = 1_048_576;
    public const int DefaultMaxRetainedArrayCount = 4;
    public const long DefaultMaxRetainedBytes = 128L * 1024L * 1024L;

    private readonly object sync = new();
    private readonly ArrayPool<byte> fallback;
    private readonly List<byte[]> retainedArrays = [];
    private long retainedBytes;
    private long rentCount;
    private long returnCount;
    private long missCount;

    public RadarProcessingRetainedPayloadByteArrayPool(
        ArrayPool<byte>? fallback = null,
        int largeArrayThreshold = DefaultLargeArrayThreshold,
        int maxRetainedArrayCount = DefaultMaxRetainedArrayCount,
        long maxRetainedBytes = DefaultMaxRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(largeArrayThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedArrayCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedBytes);

        this.fallback = fallback ?? ArrayPool<byte>.Shared;
        LargeArrayThreshold = largeArrayThreshold;
        MaxRetainedArrayCount = maxRetainedArrayCount;
        MaxRetainedBytes = maxRetainedBytes;
    }

    public int LargeArrayThreshold { get; }

    public int MaxRetainedArrayCount { get; }

    public long MaxRetainedBytes { get; }

    public int RetainedArrayCount
    {
        get
        {
            lock (sync)
            {
                return retainedArrays.Count;
            }
        }
    }

    public long RetainedBytes
    {
        get
        {
            lock (sync)
            {
                return retainedBytes;
            }
        }
    }

    public long RentCount
    {
        get
        {
            lock (sync)
            {
                return rentCount;
            }
        }
    }

    public long ReturnCount
    {
        get
        {
            lock (sync)
            {
                return returnCount;
            }
        }
    }

    public long MissCount
    {
        get
        {
            lock (sync)
            {
                return missCount;
            }
        }
    }

    public override byte[] Rent(int minimumLength)
    {
        return RentCore(minimumLength, out _);
    }

    internal byte[] RentWithMissTelemetry(
        int minimumLength,
        out bool missed)
    {
        return RentCore(minimumLength, out missed);
    }

    private byte[] RentCore(
        int minimumLength,
        out bool missed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
        missed = false;

        if (minimumLength < LargeArrayThreshold)
        {
            lock (sync)
            {
                rentCount++;
            }

            return fallback.Rent(minimumLength);
        }

        lock (sync)
        {
            rentCount++;
            var bestIndex = -1;
            var bestLength = int.MaxValue;
            for (var i = 0; i < retainedArrays.Count; i++)
            {
                var candidate = retainedArrays[i];
                if (candidate.Length < minimumLength ||
                    candidate.Length >= bestLength)
                {
                    continue;
                }

                bestIndex = i;
                bestLength = candidate.Length;
            }

            if (bestIndex >= 0)
            {
                var rented = retainedArrays[bestIndex];
                retainedArrays.RemoveAt(bestIndex);
                retainedBytes -= rented.Length;
                return rented;
            }

            missCount++;
            missed = true;
        }

        return new byte[RoundLargeArrayLength(minimumLength)];
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (array.Length < LargeArrayThreshold)
        {
            lock (sync)
            {
                returnCount++;
            }

            fallback.Return(array, clearArray);
            return;
        }

        if (clearArray)
        {
            Array.Clear(array);
        }

        lock (sync)
        {
            returnCount++;
            if (MaxRetainedArrayCount == 0 ||
                array.Length > MaxRetainedBytes)
            {
                return;
            }

            retainedArrays.Add(array);
            retainedBytes += array.Length;
            while (retainedArrays.Count > MaxRetainedArrayCount ||
                   retainedBytes > MaxRetainedBytes)
            {
                RemoveSmallestRetainedArrayUnsafe();
            }
        }
    }

    private void RemoveSmallestRetainedArrayUnsafe()
    {
        var smallestIndex = 0;
        var smallestLength = retainedArrays[0].Length;
        for (var i = 1; i < retainedArrays.Count; i++)
        {
            var candidateLength = retainedArrays[i].Length;
            if (candidateLength >= smallestLength)
            {
                continue;
            }

            smallestIndex = i;
            smallestLength = candidateLength;
        }

        retainedArrays.RemoveAt(smallestIndex);
        retainedBytes -= smallestLength;
    }

    private int RoundLargeArrayLength(int minimumLength)
    {
        var length = LargeArrayThreshold == 0 ? 1 : LargeArrayThreshold;
        while (length < minimumLength)
        {
            if (length > int.MaxValue / 2)
            {
                return minimumLength;
            }

            length *= 2;
        }

        return length;
    }
}
