using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveFileInspector
{
    private const int ArchiveTwoVolumeHeaderLength = 24;
    private const int ProbeLength = 4096;
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;

    public NexradArchiveFileInspector()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public NexradArchiveFileInspector(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

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
                compressedRecords.Diagnostic,
                compressedRecords.MessageSummary);
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

    private async Task<(
        IReadOnlyList<ArchiveTwoCompressedRecordSummary> Records,
        string? Diagnostic,
        ArchiveTwoMessageSummary MessageSummary)> ReadCompressedRecordSummariesAsync(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var records = new List<ArchiveTwoCompressedRecordSummary>();
        var controlWordBuffer = new byte[4];
        var outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        var decompressionSession = decompressor.CreateSession();
        var messageSummaryBuilder = new ArchiveTwoMessageSummaryBuilder();
        var messageScanner = new ArchiveTwoMessageStreamScanner(messageSummaryBuilder);
        byte[]? compressedPayloadBuffer = null;

        try
        {
            await using var stream = File.OpenRead(fileInfo.FullName);
            stream.Position = ArchiveTwoVolumeHeaderLength;

            while (stream.Position < stream.Length)
            {
                var controlWordOffset = stream.Position;
                var remainingBytes = stream.Length - stream.Position;
                if (remainingBytes < 4)
                {
                    return (records, $"Trailing {remainingBytes} byte(s) after compressed records cannot contain a control word.", messageSummaryBuilder.Build());
                }

                await ReadExactlyAsync(stream, controlWordBuffer, cancellationToken);
                var controlWord = BinaryPrimitives.ReadInt32BigEndian(controlWordBuffer);
                if (controlWord == int.MinValue)
                {
                    return (records, $"Compressed record at offset {controlWordOffset} has an unsupported control word value.", messageSummaryBuilder.Build());
                }

                var compressedSizeBytes = Math.Abs(controlWord);
                if (compressedSizeBytes == 0)
                {
                    return (records, $"Compressed record at offset {controlWordOffset} has zero compressed bytes.", messageSummaryBuilder.Build());
                }

                if (compressedSizeBytes > stream.Length - stream.Position)
                {
                    return (records, $"Compressed record at offset {controlWordOffset} declares {compressedSizeBytes} bytes, but only {stream.Length - stream.Position} remain.", messageSummaryBuilder.Build());
                }

                compressedPayloadBuffer = EnsureBufferCapacity(compressedPayloadBuffer, compressedSizeBytes);
                await ReadExactlyAsync(stream, compressedPayloadBuffer.AsMemory(0, compressedSizeBytes), cancellationToken);
                var startsWithBZip2Signature = StartsWithBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
                var decompression = startsWithBZip2Signature
                    ? TryDecompressAndScan(
                        decompressionSession,
                        messageScanner,
                        compressedPayloadBuffer,
                        compressedSizeBytes,
                        records.Count + 1,
                        outputBuffer)
                    : (DecompressedSizeBytes: (long?)null, Diagnostic: "Compressed payload does not start with a BZip2 signature.");

                records.Add(new ArchiveTwoCompressedRecordSummary(
                    records.Count + 1,
                    controlWordOffset,
                    controlWord,
                    compressedSizeBytes,
                    startsWithBZip2Signature,
                    decompression.DecompressedSizeBytes,
                    decompression.Diagnostic));
            }
        }
        finally
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }

        return (records, null, messageSummaryBuilder.Build());
    }

    private static bool StartsWithBZip2Signature(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 3 &&
        buffer[0] == (byte)'B' &&
        buffer[1] == (byte)'Z' &&
        buffer[2] == (byte)'h';

    private static byte[] EnsureBufferCapacity(byte[]? buffer, int requiredLength)
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

    private static (long? DecompressedSizeBytes, string? Diagnostic) TryDecompressAndScan(
        IArchiveBZip2DecompressionSession decompressionSession,
        ArchiveTwoMessageStreamScanner messageScanner,
        byte[] compressedPayload,
        int compressedSizeBytes,
        int sourceRecordSequenceNumber,
        byte[] outputBuffer)
    {
        try
        {
            messageScanner.Reset(sourceRecordSequenceNumber);
            var decompressedBytes = decompressionSession.Decompress(
                compressedPayload,
                compressedSizeBytes,
                outputBuffer,
                messageScanner.Append);
            messageScanner.Complete();
            return (decompressedBytes, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex.Message);
        }
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



