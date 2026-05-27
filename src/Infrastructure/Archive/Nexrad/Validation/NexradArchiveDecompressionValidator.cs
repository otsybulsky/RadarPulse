using System.Buffers;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveDecompressionValidator
{
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor candidateDecompressor;
    private readonly IArchiveBZip2Decompressor referenceDecompressor;

    public NexradArchiveDecompressionValidator()
        : this(
            ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName),
            ArchiveBZip2Decompressors.Create(SharpZipLibArchiveBZip2Decompressor.DecompressorName))
    {
    }

    public NexradArchiveDecompressionValidator(
        IArchiveBZip2Decompressor candidateDecompressor,
        IArchiveBZip2Decompressor referenceDecompressor)
    {
        this.candidateDecompressor = candidateDecompressor ?? throw new ArgumentNullException(nameof(candidateDecompressor));
        this.referenceDecompressor = referenceDecompressor ?? throw new ArgumentNullException(nameof(referenceDecompressor));
    }

    public ArchiveTwoDecompressionValidationResult ValidateFile(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        ArchiveTwoDecompressionValidationFileResult[] files = ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo)
            ? [ValidateArchiveTwoFile(fileInfo, cancellationToken)]
            : Array.Empty<ArchiveTwoDecompressionValidationFileResult>();

        return new ArchiveTwoDecompressionValidationResult(
            candidateDecompressor.Name,
            referenceDecompressor.Name,
            ExaminedFileCount: 1,
            SkippedFileCount: files.Length == 0 ? 1 : 0,
            files);
    }

    public ArchiveTwoDecompressionValidationResult ValidateCache(
        string cachePath,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Max files must be greater than zero.");
        }

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var files = new List<ArchiveTwoDecompressionValidationFileResult>();
        var examinedFiles = 0;
        var skippedFiles = 0;

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (files.Count >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, normalizedRadarId))
            {
                continue;
            }

            examinedFiles++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                skippedFiles++;
                continue;
            }

            files.Add(ValidateArchiveTwoFile(fileInfo, cancellationToken));
        }

        return new ArchiveTwoDecompressionValidationResult(
            candidateDecompressor.Name,
            referenceDecompressor.Name,
            examinedFiles,
            skippedFiles,
            files);
    }

    private ArchiveTwoDecompressionValidationFileResult ValidateArchiveTwoFile(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var controlWordBuffer = new byte[4];
        var candidateOutputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        var referenceOutputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        byte[]? compressedPayloadBuffer = null;
        var candidateSession = candidateDecompressor.CreateSession();
        var referenceSession = referenceDecompressor.CreateSession();
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        try
        {
            using var stream = File.OpenRead(fileInfo.FullName);
            if (stream.Length < ArchiveTwoFileReader.VolumeHeaderLength)
            {
                return Failed(fileInfo, "File is shorter than the 24-byte Archive Two volume header.");
            }

            stream.Position = ArchiveTwoFileReader.VolumeHeaderLength;
            while (stream.Position < stream.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var controlWordOffset = stream.Position;
                var compressedSizeBytes = ArchiveTwoFileReader.ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
                compressedPayloadBuffer = ArchiveTwoFileReader.EnsurePooledBufferCapacity(compressedPayloadBuffer, compressedSizeBytes);
                ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));

                if (!ArchiveTwoFileReader.StartsWithBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes)))
                {
                    return Failed(fileInfo, $"Compressed record {compressedRecordCount + 1} at offset {controlWordOffset} does not start with a BZip2 signature.");
                }

                var candidateHash = new StreamingHash();
                var candidateBytes = candidateSession.Decompress(
                    compressedPayloadBuffer,
                    compressedSizeBytes,
                    candidateOutputBuffer,
                    candidateHash.Append);

                var referenceHash = new StreamingHash();
                var referenceBytes = referenceSession.Decompress(
                    compressedPayloadBuffer,
                    compressedSizeBytes,
                    referenceOutputBuffer,
                    referenceHash.Append);

                if (candidateBytes != referenceBytes)
                {
                    return Failed(fileInfo, $"Compressed record {compressedRecordCount + 1} byte-count mismatch: {candidateDecompressor.Name}={candidateBytes}, {referenceDecompressor.Name}={referenceBytes}.");
                }

                if (candidateHash.Value != referenceHash.Value)
                {
                    return Failed(fileInfo, $"Compressed record {compressedRecordCount + 1} streaming hash mismatch: {candidateDecompressor.Name}=0x{candidateHash.Value:X16}, {referenceDecompressor.Name}=0x{referenceHash.Value:X16}.");
                }

                compressedRecordCount++;
                compressedBytes += compressedSizeBytes;
                decompressedBytes += candidateBytes;
            }

            return new ArchiveTwoDecompressionValidationFileResult(
                fileInfo.FullName,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes,
                Diagnostic: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(fileInfo, ex.Message);
        }
        finally
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(candidateOutputBuffer);
            ArrayPool<byte>.Shared.Return(referenceOutputBuffer);
        }
    }

    private static ArchiveTwoDecompressionValidationFileResult Failed(FileInfo fileInfo, string diagnostic) =>
        new(fileInfo.FullName, 0, 0, 0, diagnostic);

    private static bool MatchesRadar(FileInfo fileInfo, string? radarId)
    {
        if (radarId is null)
        {
            return true;
        }

        return fileInfo.Name.StartsWith(radarId, StringComparison.OrdinalIgnoreCase) ||
            fileInfo.DirectoryName?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, radarId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private sealed class StreamingHash
    {
        private const ulong Offset = 14695981039346656037;
        private const ulong Prime = 1099511628211;

        public ulong Value { get; private set; } = Offset;

        public void Append(ReadOnlySpan<byte> chunk)
        {
            foreach (var value in chunk)
            {
                Value ^= value;
                Value *= Prime;
            }
        }
    }
}
