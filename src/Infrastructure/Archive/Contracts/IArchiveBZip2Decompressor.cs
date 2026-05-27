namespace RadarPulse.Infrastructure.Archive;

public delegate void ArchiveBZip2DecompressedChunkHandler(ReadOnlySpan<byte> chunk);

public interface IArchiveBZip2Decompressor
{
    string Name { get; }

    IArchiveBZip2DecompressionSession CreateSession();

    long Decompress(
        byte[] compressedPayload,
        int compressedSizeBytes,
        byte[] outputBuffer,
        ArchiveBZip2DecompressedChunkHandler? chunkHandler);

    long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer);
}

public interface IArchiveBZip2DecompressionSession
{
    long Decompress(
        byte[] compressedPayload,
        int compressedSizeBytes,
        byte[] outputBuffer,
        ArchiveBZip2DecompressedChunkHandler? chunkHandler);

    long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer);
}
