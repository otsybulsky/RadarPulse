namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Deterministic checksum helper for stream identity, dictionary, source-universe, and batch metric contracts.
/// </summary>
internal static class RadarStreamChecksum
{
    /// <summary>
    /// Initial FNV-1a 64-bit offset basis used by streaming checksum calculations.
    /// </summary>
    public const ulong Initial = 14_695_981_039_346_656_037UL;

    private const ulong Prime = 1_099_511_628_211UL;

    /// <summary>
    /// Appends one byte to an existing checksum.
    /// </summary>
    public static ulong AppendByte(ulong checksum, byte value) =>
        unchecked((checksum ^ value) * Prime);

    /// <summary>
    /// Appends a signed 32-bit integer as little-endian bytes.
    /// </summary>
    public static ulong AppendInt32(ulong checksum, int value) =>
        AppendUInt32(checksum, unchecked((uint)value));

    /// <summary>
    /// Appends an unsigned 16-bit integer as little-endian bytes.
    /// </summary>
    public static ulong AppendUInt16(ulong checksum, ushort value)
    {
        checksum = AppendByte(checksum, (byte)value);
        return AppendByte(checksum, (byte)(value >> 8));
    }

    /// <summary>
    /// Appends an unsigned 32-bit integer as little-endian bytes.
    /// </summary>
    public static ulong AppendUInt32(ulong checksum, uint value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        return AppendByte(checksum, (byte)(value >> 24));
    }

    /// <summary>
    /// Appends a signed 64-bit integer as little-endian bytes.
    /// </summary>
    public static ulong AppendInt64(ulong checksum, long value) =>
        AppendUInt64(checksum, unchecked((ulong)value));

    /// <summary>
    /// Appends an unsigned 64-bit integer as little-endian bytes.
    /// </summary>
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

    /// <summary>
    /// Appends the IEEE 754 bit representation of a single-precision value.
    /// </summary>
    public static ulong AppendSingle(ulong checksum, float value) =>
        AppendUInt32(checksum, BitConverter.SingleToUInt32Bits(value));

    /// <summary>
    /// Appends a string length and UTF-16 ordinal characters to an existing checksum.
    /// </summary>
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
