using System.Buffers;
using System.Buffers.Binary;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveDecompressionValidator
{
    private const int ArchiveTwoVolumeHeaderLength = 24;
    private const int BZip2SignatureLength = 3;
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

        ArchiveTwoDecompressionValidationFileResult[] files = IsArchiveTwoBaseData(fileInfo)
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
            if (!IsArchiveTwoBaseData(fileInfo))
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
            if (stream.Length < ArchiveTwoVolumeHeaderLength)
            {
                return Failed(fileInfo, "File is shorter than the 24-byte Archive Two volume header.");
            }

            stream.Position = ArchiveTwoVolumeHeaderLength;
            while (stream.Position < stream.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var controlWordOffset = stream.Position;
                var compressedSizeBytes = ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
                compressedPayloadBuffer = EnsureBufferCapacity(compressedPayloadBuffer, compressedSizeBytes);
                ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));

                if (!StartsWithBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes)))
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

    private static bool IsArchiveTwoBaseData(FileInfo fileInfo)
    {
        if (fileInfo.Length < 4)
        {
            return false;
        }

        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, signature);
        return signature[0] == (byte)'A' &&
            signature[1] == (byte)'R' &&
            signature[2] == (byte)'2' &&
            signature[3] == (byte)'V';
    }

    private static int ReadCompressedRecordSize(
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

        return compressedSizeBytes;
    }

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

    private static bool StartsWithBZip2Signature(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= BZip2SignatureLength &&
        buffer[0] == (byte)'B' &&
        buffer[1] == (byte)'Z' &&
        buffer[2] == (byte)'h';

    private static void ReadExactly(Stream stream, Span<byte> buffer)
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
