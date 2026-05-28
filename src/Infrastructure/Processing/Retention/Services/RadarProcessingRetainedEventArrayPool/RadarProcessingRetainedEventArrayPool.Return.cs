using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingRetainedEventArrayPool
{
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
}
