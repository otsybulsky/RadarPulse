using System.Buffers;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveFileInspector
{
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

        if (ArchiveTwoFileReader.StartsWithArchiveTwoSignature(probe))
        {
            if (probe.Length < ArchiveTwoFileReader.VolumeHeaderLength)
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
                ArchiveTwoFileReader.ParseVolumeHeader(probe.AsSpan(0, ArchiveTwoFileReader.VolumeHeaderLength)),
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

    private static bool LooksLikeMdmOrCompressedStream(string filePath, ReadOnlySpan<byte> buffer) =>
        Path.GetFileName(filePath).Contains("_MDM", StringComparison.OrdinalIgnoreCase) ||
        ContainsBZip2Signature(buffer);

    private static bool ContainsBZip2Signature(ReadOnlySpan<byte> buffer)
    {
        for (var i = 0; i <= buffer.Length - ArchiveTwoFileReader.BZip2SignatureLength; i++)
        {
            if (ArchiveTwoFileReader.StartsWithBZip2Signature(buffer[i..]))
            {
                return true;
            }
        }

        return false;
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
            stream.Position = ArchiveTwoFileReader.VolumeHeaderLength;

            while (stream.Position < stream.Length)
            {
                var controlWordOffset = stream.Position;
                ArchiveTwoCompressedRecordHeader recordHeader;
                try
                {
                    recordHeader = await ArchiveTwoFileReader.ReadCompressedRecordHeaderAsync(
                        stream,
                        controlWordBuffer,
                        controlWordOffset,
                        cancellationToken);
                }
                catch (InvalidDataException ex)
                {
                    return (records, ex.Message, messageSummaryBuilder.Build());
                }

                var compressedSizeBytes = recordHeader.CompressedSizeBytes;
                compressedPayloadBuffer = ArchiveTwoFileReader.EnsurePooledBufferCapacity(
                    compressedPayloadBuffer,
                    compressedSizeBytes);
                await ArchiveTwoFileReader.ReadExactlyAsync(
                    stream,
                    compressedPayloadBuffer.AsMemory(0, compressedSizeBytes),
                    cancellationToken);
                var startsWithBZip2Signature = ArchiveTwoFileReader.StartsWithBZip2Signature(
                    compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
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
                    recordHeader.ControlWord,
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
}



