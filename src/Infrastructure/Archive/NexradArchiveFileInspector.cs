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
                    "Archive Two signature is present, but the file is shorter than the 24-byte volume header.");
            }

            return new NexradArchiveFileInspection(
                filePath,
                fileInfo.Length,
                NexradArchiveFileKind.ArchiveTwoBaseData,
                ParseArchiveTwoVolumeHeader(probe.AsSpan(0, ArchiveTwoVolumeHeaderLength)),
                null);
        }

        if (LooksLikeMdmOrCompressedStream(filePath, probe))
        {
            return new NexradArchiveFileInspection(
                filePath,
                fileInfo.Length,
                NexradArchiveFileKind.MdmOrCompressedStream,
                null,
                "File does not start with an Archive Two volume header and should not be parsed as base-data volume.");
        }

        return new NexradArchiveFileInspection(
            filePath,
            fileInfo.Length,
            NexradArchiveFileKind.Unknown,
            null,
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
}



