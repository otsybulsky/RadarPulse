using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingRetainedEventArrayPool
{
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
