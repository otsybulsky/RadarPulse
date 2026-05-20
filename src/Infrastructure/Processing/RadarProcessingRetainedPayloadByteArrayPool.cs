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

    public override byte[] Rent(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
        if (minimumLength < LargeArrayThreshold)
        {
            return fallback.Rent(minimumLength);
        }

        lock (sync)
        {
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
        }

        return new byte[RoundLargeArrayLength(minimumLength)];
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (array.Length < LargeArrayThreshold)
        {
            fallback.Return(array, clearArray);
            return;
        }

        if (clearArray)
        {
            Array.Clear(array);
        }

        lock (sync)
        {
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
