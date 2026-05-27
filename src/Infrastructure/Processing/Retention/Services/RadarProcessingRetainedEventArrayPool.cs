using System.Buffers;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Array pool that retains a bounded number of large radar event arrays.
/// </summary>
/// <remarks>
/// Small arrays delegate to the fallback pool. Large arrays are retained by
/// size-bounded best fit so retained payload snapshots can reuse event buffers
/// without unbounded memory growth.
/// </remarks>
public sealed class RadarProcessingRetainedEventArrayPool : ArrayPool<RadarStreamEvent>
{
    /// <summary>
    /// Default threshold at which event arrays are retained by this pool.
    /// </summary>
    public const int DefaultLargeArrayThreshold = 16_384;
    /// <summary>
    /// Default maximum count of retained large event arrays.
    /// </summary>
    public const int DefaultMaxRetainedArrayCount = 8;
    /// <summary>
    /// Default maximum bytes retained by large event arrays.
    /// </summary>
    public const long DefaultMaxRetainedBytes = 128L * 1024L * 1024L;

    private readonly object sync = new();
    private readonly ArrayPool<RadarStreamEvent> fallback;
    private readonly List<RadarStreamEvent[]> retainedArrays = [];
    private long retainedBytes;
    private long rentCount;
    private long returnCount;
    private long missCount;

    /// <summary>
    /// Creates a retained event array pool with bounded large-array retention.
    /// </summary>
    public RadarProcessingRetainedEventArrayPool(
        ArrayPool<RadarStreamEvent>? fallback = null,
        int largeArrayThreshold = DefaultLargeArrayThreshold,
        int maxRetainedArrayCount = DefaultMaxRetainedArrayCount,
        long maxRetainedBytes = DefaultMaxRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(largeArrayThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedArrayCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedBytes);

        this.fallback = fallback ?? ArrayPool<RadarStreamEvent>.Shared;
        LargeArrayThreshold = largeArrayThreshold;
        MaxRetainedArrayCount = maxRetainedArrayCount;
        MaxRetainedBytes = maxRetainedBytes;
    }

    /// <summary>
    /// Minimum requested event count retained by this pool instead of the fallback pool.
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
    /// Total event slots currently retained by large arrays.
    /// </summary>
    public long RetainedEventCount
    {
        get
        {
            lock (sync)
            {
                return retainedBytes / RadarStreamEvent.SizeInBytes;
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
    /// Rents an event array, using retained large arrays when available.
    /// </summary>
    public override RadarStreamEvent[] Rent(int minimumLength)
    {
        return RentCore(minimumLength, out _);
    }

    internal RadarStreamEvent[] RentWithMissTelemetry(
        int minimumLength,
        out bool missed)
    {
        return RentCore(minimumLength, out missed);
    }

    /// <summary>
    /// Seeds the retained large-array cache with arrays sized for the requested event count.
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
        var arrayBytes = GetArrayBytes(arrayLength);
        if (arrayBytes > MaxRetainedBytes)
        {
            return;
        }

        var retainedByBudget = MaxRetainedBytes / arrayBytes;
        var retainedCount = (int)Math.Min(
            arrayCount,
            Math.Min(MaxRetainedArrayCount, retainedByBudget));
        if (retainedCount == 0)
        {
            return;
        }

        var arrays = new RadarStreamEvent[retainedCount][];
        for (var i = 0; i < arrays.Length; i++)
        {
            arrays[i] = new RadarStreamEvent[arrayLength];
        }

        lock (sync)
        {
            foreach (var array in arrays)
            {
                retainedArrays.Add(array);
                retainedBytes = checked(retainedBytes + arrayBytes);
            }

            while (retainedArrays.Count > MaxRetainedArrayCount ||
                   retainedBytes > MaxRetainedBytes)
            {
                RemoveSmallestRetainedArrayUnsafe();
            }
        }
    }

    /// <summary>
    /// Returns an event array, retaining large arrays within configured count and byte budgets.
    /// </summary>
    public override void Return(RadarStreamEvent[] array, bool clearArray = false)
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
            var arrayBytes = GetArrayBytes(array.Length);
            if (MaxRetainedArrayCount == 0 ||
                arrayBytes > MaxRetainedBytes)
            {
                return;
            }

            retainedArrays.Add(array);
            retainedBytes = checked(retainedBytes + arrayBytes);
            while (retainedArrays.Count > MaxRetainedArrayCount ||
                   retainedBytes > MaxRetainedBytes)
            {
                RemoveSmallestRetainedArrayUnsafe();
            }
        }
    }

    private RadarStreamEvent[] RentCore(
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
                retainedBytes -= GetArrayBytes(rented.Length);
                return rented;
            }

            missCount++;
            missed = true;
        }

        return new RadarStreamEvent[RoundLargeArrayLength(minimumLength)];
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
        retainedBytes -= GetArrayBytes(smallestLength);
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

    private static long GetArrayBytes(int arrayLength) =>
        checked((long)arrayLength * RadarStreamEvent.SizeInBytes);
}
