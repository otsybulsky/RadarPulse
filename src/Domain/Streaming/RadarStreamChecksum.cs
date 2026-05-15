namespace RadarPulse.Domain.Streaming;

internal static class RadarStreamChecksum
{
    public const ulong Initial = 14_695_981_039_346_656_037UL;

    private const ulong Prime = 1_099_511_628_211UL;

    public static ulong AppendByte(ulong checksum, byte value) =>
        unchecked((checksum ^ value) * Prime);

    public static ulong AppendInt32(ulong checksum, int value) =>
        AppendUInt32(checksum, unchecked((uint)value));

    public static ulong AppendUInt16(ulong checksum, ushort value)
    {
        checksum = AppendByte(checksum, (byte)value);
        return AppendByte(checksum, (byte)(value >> 8));
    }

    public static ulong AppendUInt32(ulong checksum, uint value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        return AppendByte(checksum, (byte)(value >> 24));
    }

    public static ulong AppendInt64(ulong checksum, long value) =>
        AppendUInt64(checksum, unchecked((ulong)value));

    public static ulong AppendUInt64(ulong checksum, ulong value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        checksum = AppendByte(checksum, (byte)(value >> 24));
        checksum = AppendByte(checksum, (byte)(value >> 32));
        checksum = AppendByte(checksum, (byte)(value >> 40));
        checksum = AppendByte(checksum, (byte)(value >> 48));
        return AppendByte(checksum, (byte)(value >> 56));
    }

    public static ulong AppendSingle(ulong checksum, float value) =>
        AppendUInt32(checksum, BitConverter.SingleToUInt32Bits(value));

    public static ulong AppendStringOrdinal(ulong checksum, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        checksum = AppendInt32(checksum, value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            checksum = AppendUInt16(checksum, value[i]);
        }

        return checksum;
    }
}
