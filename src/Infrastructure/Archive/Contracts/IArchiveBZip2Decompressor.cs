namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Receives decompressed bytes as reusable chunks during archive BZip2 decompression.
/// </summary>
public delegate void ArchiveBZip2DecompressedChunkHandler(ReadOnlySpan<byte> chunk);

/// <summary>
/// Factory and one-shot facade for Archive II BZip2 decompression implementations.
/// </summary>
public interface IArchiveBZip2Decompressor
{
    /// <summary>
    /// Gets the stable decompressor name used by CLI options, benchmarks, and result records.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates a reusable decompression session with implementation-specific scratch state.
    /// </summary>
    IArchiveBZip2DecompressionSession CreateSession();

    /// <summary>
    /// Decompresses one compressed Archive II record and emits decompressed chunks through the supplied buffer.
    /// </summary>
    long Decompress(
        byte[] compressedPayload,
        int compressedSizeBytes,
        byte[] outputBuffer,
        ArchiveBZip2DecompressedChunkHandler? chunkHandler);

    /// <summary>
    /// Decompresses one compressed Archive II record only to count produced bytes.
    /// </summary>
    long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer);
}

/// <summary>
/// Reusable Archive II BZip2 decompression session.
/// </summary>
public interface IArchiveBZip2DecompressionSession
{
    /// <summary>
    /// Decompresses one compressed Archive II record and emits decompressed chunks through the supplied buffer.
    /// </summary>
    long Decompress(
        byte[] compressedPayload,
        int compressedSizeBytes,
        byte[] outputBuffer,
        ArchiveBZip2DecompressedChunkHandler? chunkHandler);

    /// <summary>
    /// Decompresses one compressed Archive II record only to count produced bytes.
    /// </summary>
    long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer);
}
