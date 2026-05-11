using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

internal static class ArchiveTwoFileReader
{
    public const int VolumeHeaderLength = 24;
    public const int BZip2SignatureLength = 3;

    public static bool IsArchiveTwoBaseData(FileInfo fileInfo)
    {
        if (fileInfo.Length < 4)
        {
            return false;
        }

        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, signature);
        return StartsWithArchiveTwoSignature(signature);
    }

    public static bool StartsWithArchiveTwoSignature(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 4 &&
        buffer[0] == (byte)'A' &&
        buffer[1] == (byte)'R' &&
        buffer[2] == (byte)'2' &&
        buffer[3] == (byte)'V';

    public static ArchiveTwoVolumeHeader ReadVolumeHeader(FileInfo fileInfo)
    {
        if (fileInfo.Length < VolumeHeaderLength)
        {
            throw new InvalidDataException("File is shorter than the 24-byte Archive Two volume header.");
        }

        Span<byte> header = stackalloc byte[VolumeHeaderLength];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, header);
        return ParseVolumeHeader(header);
    }

    public static void ValidateVolumeHeaderSignature(FileInfo fileInfo)
    {
        if (fileInfo.Length < VolumeHeaderLength)
        {
            throw new InvalidDataException("File is shorter than the 24-byte Archive Two volume header.");
        }

        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, signature);
        if (!StartsWithArchiveTwoSignature(signature))
        {
            throw new InvalidDataException("File does not start with an Archive Two volume header.");
        }
    }

    public static ArchiveTwoVolumeHeader ParseVolumeHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < VolumeHeaderLength)
        {
            throw new InvalidDataException("File is shorter than the 24-byte Archive Two volume header.");
        }

        header = header[..VolumeHeaderLength];
        if (!StartsWithArchiveTwoSignature(header))
        {
            throw new InvalidDataException("File does not start with an Archive Two volume header.");
        }

        var archiveFilename = Encoding.ASCII.GetString(header[..12]);
        var version = Encoding.ASCII.GetString(header.Slice(6, 2));
        var extensionText = Encoding.ASCII.GetString(header.Slice(9, 3));
        if (!int.TryParse(extensionText, out var extensionNumber))
        {
            throw new FormatException($"Archive Two volume extension is not numeric: {extensionText}");
        }

        var nexradModifiedJulianDate = BinaryPrimitives.ReadInt32BigEndian(header.Slice(12, 4));
        var millisecondsPastMidnight = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var radarId = Encoding.ASCII.GetString(header.Slice(20, 4)).TrimEnd('\0', ' ');
        var volumeDate = DateOnly.FromDayNumber(
            new DateOnly(1970, 1, 1).DayNumber + nexradModifiedJulianDate - 1);
        var volumeTime = TimeSpan.FromMilliseconds(millisecondsPastMidnight);

        return new ArchiveTwoVolumeHeader(
            archiveFilename,
            version,
            extensionNumber,
            volumeDate,
            volumeTime,
            new DateTimeOffset(volumeDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).Add(volumeTime),
            radarId);
    }

    public static IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> ReadCompressedRecordDescriptors(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var records = new List<ArchiveTwoCompressedRecordDescriptor>();
        var controlWordBuffer = new byte[4];
        Span<byte> signature = stackalloc byte[BZip2SignatureLength];

        using var stream = File.OpenRead(fileInfo.FullName);
        stream.Position = VolumeHeaderLength;

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controlWordOffset = stream.Position;
            var compressedSizeBytes = ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
            if (compressedSizeBytes < BZip2SignatureLength)
            {
                throw new InvalidDataException($"Compressed record at offset {controlWordOffset} is too short to contain a BZip2 signature.");
            }

            var payloadOffset = stream.Position;
            ReadExactly(stream, signature);
            ValidateBZip2Signature(signature, controlWordOffset);
            stream.Position += compressedSizeBytes - BZip2SignatureLength;

            records.Add(new ArchiveTwoCompressedRecordDescriptor(
                records.Count,
                controlWordOffset,
                payloadOffset,
                compressedSizeBytes));
        }

        return records;
    }

    public static ArchiveTwoCompressedRecordHeader ReadCompressedRecordHeader(
        Stream stream,
        byte[] controlWordBuffer,
        long controlWordOffset)
    {
        var remainingBytes = stream.Length - stream.Position;
        if (remainingBytes < 4)
        {
            throw new InvalidDataException($"Trailing {remainingBytes} byte(s) after compressed records cannot contain a control word.");
        }

        ReadExactly(stream, controlWordBuffer);
        return DecodeCompressedRecordHeader(stream, controlWordBuffer, controlWordOffset);
    }

    public static async ValueTask<ArchiveTwoCompressedRecordHeader> ReadCompressedRecordHeaderAsync(
        Stream stream,
        byte[] controlWordBuffer,
        long controlWordOffset,
        CancellationToken cancellationToken)
    {
        var remainingBytes = stream.Length - stream.Position;
        if (remainingBytes < 4)
        {
            throw new InvalidDataException($"Trailing {remainingBytes} byte(s) after compressed records cannot contain a control word.");
        }

        await ReadExactlyAsync(stream, controlWordBuffer, cancellationToken);
        return DecodeCompressedRecordHeader(stream, controlWordBuffer, controlWordOffset);
    }

    public static int ReadCompressedRecordSize(
        Stream stream,
        byte[] controlWordBuffer,
        long controlWordOffset) =>
        ReadCompressedRecordHeader(stream, controlWordBuffer, controlWordOffset).CompressedSizeBytes;

    public static bool StartsWithBZip2Signature(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= BZip2SignatureLength &&
        buffer[0] == (byte)'B' &&
        buffer[1] == (byte)'Z' &&
        buffer[2] == (byte)'h';

    public static void ValidateBZip2Signature(ReadOnlySpan<byte> buffer, long controlWordOffset)
    {
        if (!StartsWithBZip2Signature(buffer))
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} does not start with a BZip2 signature.");
        }
    }

    public static byte[] EnsurePooledBufferCapacity(byte[]? buffer, int requiredLength)
    {
        if (buffer is not null && buffer.Length >= requiredLength)
        {
            return buffer;
        }

        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return ArrayPool<byte>.Shared.Rent(requiredLength);
    }

    public static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = stream.Read(buffer[totalBytesRead..]);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected end of NEXRAD archive file.");
            }

            totalBytesRead += bytesRead;
        }
    }

    public static void ReadExactly(SafeFileHandle fileHandle, Span<byte> buffer, long fileOffset)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = RandomAccess.Read(fileHandle, buffer[totalBytesRead..], fileOffset + totalBytesRead);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected end of NEXRAD archive file.");
            }

            totalBytesRead += bytesRead;
        }
    }

    public static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer[totalBytesRead..], cancellationToken);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected end of NEXRAD archive file.");
            }

            totalBytesRead += bytesRead;
        }
    }

    private static ArchiveTwoCompressedRecordHeader DecodeCompressedRecordHeader(
        Stream stream,
        ReadOnlySpan<byte> controlWordBuffer,
        long controlWordOffset)
    {
        var controlWord = BinaryPrimitives.ReadInt32BigEndian(controlWordBuffer);
        if (controlWord == int.MinValue)
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} has an unsupported control word value.");
        }

        var compressedSizeBytes = Math.Abs(controlWord);
        if (compressedSizeBytes == 0)
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} has zero compressed bytes.");
        }

        if (compressedSizeBytes > stream.Length - stream.Position)
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} declares {compressedSizeBytes} bytes, but only {stream.Length - stream.Position} remain.");
        }

        return new ArchiveTwoCompressedRecordHeader(controlWord, compressedSizeBytes);
    }
}

internal readonly record struct ArchiveTwoCompressedRecordHeader(
    int ControlWord,
    int CompressedSizeBytes);

internal readonly record struct ArchiveTwoCompressedRecordDescriptor(
    int Index,
    long ControlWordOffset,
    long PayloadOffset,
    int CompressedSizeBytes);
