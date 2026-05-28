namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarEventBatchBuilder
{
    private static void EnsureValidIdentity(RadarStreamIdentity identity)
    {
        if (identity.DictionaryVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(identity));
        }

        if (identity.SourceUniverseVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(identity));
        }
    }

    private void EnsureSourceUniverseVersion(SourceUniverseVersion identitySourceUniverseVersion)
    {
        if (!hasSourceUniverseVersion)
        {
            sourceUniverseVersion = identitySourceUniverseVersion;
            hasSourceUniverseVersion = true;
            return;
        }

        if (sourceUniverseVersion != identitySourceUniverseVersion)
        {
            throw new ArgumentException("All events in one batch must use the same source-universe version.");
        }
    }

    private static void EnsureExpectedPayloadLength(
        ushort gateCount,
        RadarStreamWordSize wordSize,
        int payloadLength)
    {
        ArgumentOutOfRangeException.ThrowIfZero(gateCount);

        var bytesPerGate = wordSize switch
        {
            RadarStreamWordSize.EightBit => 1,
            RadarStreamWordSize.SixteenBit => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(wordSize))
        };
        var expectedLength = checked(gateCount * bytesPerGate);
        if (payloadLength != expectedLength)
        {
            throw new ArgumentException("Payload length must match gate count and word size.", nameof(payloadLength));
        }
    }

    private static long SumRawValues(RadarStreamWordSize wordSize, ReadOnlySpan<byte> payload)
    {
        var checksum = 0L;
        switch (wordSize)
        {
            case RadarStreamWordSize.EightBit:
                for (var valueIndex = 0; valueIndex < payload.Length; valueIndex++)
                {
                    checksum += payload[valueIndex];
                }

                return checksum;

            case RadarStreamWordSize.SixteenBit:
                for (var valueIndex = 0; valueIndex < payload.Length; valueIndex += sizeof(ushort))
                {
                    checksum += (payload[valueIndex] << 8) | payload[valueIndex + 1];
                }

                return checksum;

            default:
                throw new InvalidOperationException("Unsupported radar stream word size.");
        }
    }
}
