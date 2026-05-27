using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

internal static class ArchiveTwoGateMomentChronologyChecksum
{
    private const ulong Prime = 1_099_511_628_211UL;
    private const ulong Offset = 14_695_981_039_346_656_037UL;

    public static ulong Append(ulong checksum, ArchiveTwoGateMomentEvent gateMomentEvent)
    {
        unchecked
        {
            return checksum * Prime + HashEvent(gateMomentEvent);
        }
    }

    public static ulong Combine(ulong left, ulong right, long rightEventCount)
    {
        unchecked
        {
            return left * PowUnchecked(Prime, rightEventCount) + right;
        }
    }

    private static ulong PowUnchecked(ulong value, long exponent)
    {
        var result = 1UL;
        var factor = value;
        var remaining = exponent;
        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                unchecked
                {
                    result *= factor;
                }
            }

            remaining >>= 1;
            unchecked
            {
                factor *= factor;
            }
        }

        return result;
    }

    private static ulong HashEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
    {
        var hash = Offset;
        hash = Mix(hash, gateMomentEvent.SourceOrder.CompressedRecordSequenceNumber);
        hash = Mix(hash, gateMomentEvent.SourceOrder.MessageSequenceNumberInRecord);
        hash = Mix(hash, gateMomentEvent.SourceOrder.Type31RadialSequenceNumber);
        hash = Mix(hash, gateMomentEvent.SweepSequenceNumber);
        hash = Mix(hash, gateMomentEvent.ElevationNumber);
        hash = Mix(hash, gateMomentEvent.RadialSequenceNumber);
        hash = Mix(hash, gateMomentEvent.GateIndex);
        hash = Mix(hash, BitConverter.SingleToInt32Bits(gateMomentEvent.RangeKilometers));
        hash = Mix(hash, gateMomentEvent.MomentName);
        hash = Mix(hash, gateMomentEvent.RawValue);
        hash = Mix(hash, (int)gateMomentEvent.Status);
        if (gateMomentEvent.CalibratedValue is { } calibratedValue)
        {
            hash = Mix(hash, (long)Math.Round(calibratedValue * 1_000d, MidpointRounding.AwayFromZero));
        }

        return hash;
    }

    private static ulong Mix(ulong hash, int value) => Mix(hash, (long)value);

    private static ulong Mix(ulong hash, long value)
    {
        unchecked
        {
            return (hash ^ (ulong)value) * Prime;
        }
    }

    private static ulong Mix(ulong hash, string value)
    {
        foreach (var character in value)
        {
            hash = Mix(hash, character);
        }

        return hash;
    }
}
