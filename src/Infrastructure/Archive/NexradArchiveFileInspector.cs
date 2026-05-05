using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveFileInspector
{
    private const int ArchiveTwoVolumeHeaderLength = 24;
    private const int ProbeLength = 4096;

    public async Task<NexradArchiveFileInspection> InspectAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        var probe = new byte[Math.Min(ProbeLength, checked((int)Math.Min(fileInfo.Length, ProbeLength)))];
        await using (var stream = File.OpenRead(filePath))
        {
            var bytesRead = await stream.ReadAsync(probe, cancellationToken);
            if (bytesRead != probe.Length)
            {
                Array.Resize(ref probe, bytesRead);
            }
        }

        if (StartsWithArchiveTwoSignature(probe))
        {
            if (probe.Length < ArchiveTwoVolumeHeaderLength)
            {
                return new NexradArchiveFileInspection(
                    filePath,
                    fileInfo.Length,
                    NexradArchiveFileKind.Unknown,
                    null,
                    Array.Empty<ArchiveTwoCompressedRecordSummary>(),
                    "Archive Two signature is present, but the file is shorter than the 24-byte volume header.");
            }

            var compressedRecords = await ReadCompressedRecordSummariesAsync(fileInfo, cancellationToken);
            return new NexradArchiveFileInspection(
                filePath,
                fileInfo.Length,
                NexradArchiveFileKind.ArchiveTwoBaseData,
                ParseArchiveTwoVolumeHeader(probe.AsSpan(0, ArchiveTwoVolumeHeaderLength)),
                compressedRecords.Records,
                compressedRecords.Diagnostic);
        }

        if (LooksLikeMdmOrCompressedStream(filePath, probe))
        {
            return new NexradArchiveFileInspection(
                filePath,
                fileInfo.Length,
                NexradArchiveFileKind.MdmOrCompressedStream,
                null,
                Array.Empty<ArchiveTwoCompressedRecordSummary>(),
                "File does not start with an Archive Two volume header and should not be parsed as base-data volume.");
        }

        return new NexradArchiveFileInspection(
            filePath,
            fileInfo.Length,
            NexradArchiveFileKind.Unknown,
            null,
            Array.Empty<ArchiveTwoCompressedRecordSummary>(),
            "File signature is not recognized as a supported NEXRAD archive file kind.");
    }

    private static bool StartsWithArchiveTwoSignature(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 4 &&
        buffer[0] == (byte)'A' &&
        buffer[1] == (byte)'R' &&
        buffer[2] == (byte)'2' &&
        buffer[3] == (byte)'V';

    private static bool LooksLikeMdmOrCompressedStream(string filePath, ReadOnlySpan<byte> buffer) =>
        Path.GetFileName(filePath).Contains("_MDM", StringComparison.OrdinalIgnoreCase) ||
        ContainsBZip2Signature(buffer);

    private static bool ContainsBZip2Signature(ReadOnlySpan<byte> buffer)
    {
        for (var i = 0; i <= buffer.Length - 3; i++)
        {
            if (buffer[i] == (byte)'B' &&
                buffer[i + 1] == (byte)'Z' &&
                buffer[i + 2] == (byte)'h')
            {
                return true;
            }
        }

        return false;
    }

    private static ArchiveTwoVolumeHeader ParseArchiveTwoVolumeHeader(ReadOnlySpan<byte> header)
    {
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

    private static async Task<(IReadOnlyList<ArchiveTwoCompressedRecordSummary> Records, string? Diagnostic)> ReadCompressedRecordSummariesAsync(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var records = new List<ArchiveTwoCompressedRecordSummary>();
        var controlWordBuffer = new byte[4];
        var signatureBuffer = new byte[3];

        await using var stream = File.OpenRead(fileInfo.FullName);
        stream.Position = ArchiveTwoVolumeHeaderLength;

        while (stream.Position < stream.Length)
        {
            var controlWordOffset = stream.Position;
            var remainingBytes = stream.Length - stream.Position;
            if (remainingBytes < 4)
            {
                return (records, $"Trailing {remainingBytes} byte(s) after compressed records cannot contain a control word.");
            }

            await ReadExactlyAsync(stream, controlWordBuffer, cancellationToken);
            var controlWord = BinaryPrimitives.ReadInt32BigEndian(controlWordBuffer);
            if (controlWord == int.MinValue)
            {
                return (records, $"Compressed record at offset {controlWordOffset} has an unsupported control word value.");
            }

            var compressedSizeBytes = Math.Abs(controlWord);
            if (compressedSizeBytes == 0)
            {
                return (records, $"Compressed record at offset {controlWordOffset} has zero compressed bytes.");
            }

            if (compressedSizeBytes > stream.Length - stream.Position)
            {
                return (records, $"Compressed record at offset {controlWordOffset} declares {compressedSizeBytes} bytes, but only {stream.Length - stream.Position} remain.");
            }

            var signatureBytesToRead = Math.Min(signatureBuffer.Length, compressedSizeBytes);
            var startsWithBZip2Signature = false;
            if (signatureBytesToRead > 0)
            {
                await ReadExactlyAsync(stream, signatureBuffer.AsMemory(0, signatureBytesToRead), cancellationToken);
                startsWithBZip2Signature =
                    signatureBytesToRead == 3 &&
                    signatureBuffer[0] == (byte)'B' &&
                    signatureBuffer[1] == (byte)'Z' &&
                    signatureBuffer[2] == (byte)'h';
            }

            stream.Position += compressedSizeBytes - signatureBytesToRead;
            records.Add(new ArchiveTwoCompressedRecordSummary(
                records.Count + 1,
                controlWordOffset,
                controlWord,
                compressedSizeBytes,
                startsWithBZip2Signature));
        }

        return (records, null);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
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
}



