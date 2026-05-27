using System.Buffers;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Array pool that retains a bounded number of large payload byte arrays.
/// </summary>
/// <remarks>
/// Small arrays delegate to the fallback pool. Large arrays are retained by
/// size-bounded best fit so archive-shaped retained payload copies can reuse
/// large buffers without unbounded memory growth.
/// </remarks>
public sealed class RadarProcessingRetainedPayloadByteArrayPool : ArrayPool<byte>
{
    /// <summary>
    /// Default threshold at which byte arrays are retained by this pool.
    /// </summary>
    public const int DefaultLargeArrayThreshold = 1_048_576;
    /// <summary>
    /// Default maximum count of retained large arrays.
    /// </summary>
    public const int DefaultMaxRetainedArrayCount = 4;
    /// <summary>
    /// Default maximum bytes retained by large arrays.
    /// </summary>
    public const long DefaultMaxRetainedBytes = 128L * 1024L * 1024L;

    private readonly object sync = new();
    private readonly ArrayPool<byte> fallback;
    private readonly List<byte[]> retainedArrays = [];
    private long retainedBytes;
    private long rentCount;
    private long returnCount;
    private long missCount;

    /// <summary>
    /// Creates a retained byte array pool with bounded large-array retention.
    /// </summary>
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

    /// <summary>
    /// Minimum requested length retained by this pool instead of the fallback pool.
    /// </summary>
    public int LargeArrayThreshold { get; }

    /// <summary>
    /// Maximum number of retained large arrays.
    /// </summary>
    public int MaxRetainedArrayCount { get; }

    /// <summary>
    /// Maximum retained bytes across large arrays.
    /// </summary>
    public long MaxRetainedBytes { get; }

    /// <summary>
    /// Number of large arrays currently retained.
    /// </summary>
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

    /// <summary>
    /// Total bytes currently retained by large arrays.
    /// </summary>
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

    /// <summary>
    /// Number of rent requests observed by this pool.
    /// </summary>
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

    /// <summary>
    /// Number of arrays returned to this pool.
    /// </summary>
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

    /// <summary>
    /// Number of large-array rents that missed retained buffers and allocated.
    /// </summary>
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

    /// <summary>
    /// Rents a byte array, using retained large arrays when available.
    /// </summary>
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

    /// <summary>
    /// Seeds the retained large-array cache with arrays sized for the requested payload length.
    /// </summary>
    public void Prewarm(
        int minimumLength,
        int arrayCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayCount);
        if (arrayCount == 0 ||
            minimumLength < LargeArrayThreshold ||
            MaxRetainedArrayCount == 0)
        {
            return;
        }

        var arrayLength = RoundLargeArrayLength(minimumLength);
        if (arrayLength > MaxRetainedBytes)
        {
            return;
        }

        var retainedByBudget = MaxRetainedBytes / arrayLength;
        var retainedCount = (int)Math.Min(
            arrayCount,
            Math.Min(MaxRetainedArrayCount, retainedByBudget));
        if (retainedCount == 0)
        {
            return;
        }

        var arrays = new byte[retainedCount][];
        for (var i = 0; i < arrays.Length; i++)
        {
            arrays[i] = new byte[arrayLength];
        }

        lock (sync)
        {
            foreach (var array in arrays)
            {
                retainedArrays.Add(array);
                retainedBytes += array.Length;
            }

            while (retainedArrays.Count > MaxRetainedArrayCount ||
                   retainedBytes > MaxRetainedBytes)
            {
                RemoveSmallestRetainedArrayUnsafe();
            }
        }
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

    /// <summary>
    /// Returns a byte array, retaining large arrays within configured count and byte budgets.
    /// </summary>
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
