namespace RadarPulse.Infrastructure.Archive;

public interface IArchiveBZip2Decompressor
{
    string Name { get; }

    long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer);
}
