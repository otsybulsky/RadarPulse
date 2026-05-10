namespace RadarPulse.Infrastructure.Archive;

public static class ArchiveBZip2Decompressors
{
    public const string DefaultName = SharpZipLibArchiveBZip2Decompressor.DecompressorName;

    public static IArchiveBZip2Decompressor Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return NormalizeName(name) switch
        {
            SharpCompressArchiveBZip2Decompressor.DecompressorName => new SharpCompressArchiveBZip2Decompressor(),
            SharpZipLibArchiveBZip2Decompressor.DecompressorName => new SharpZipLibArchiveBZip2Decompressor(),
            _ => throw new ArgumentException(
                $"Unknown decompressor '{name}'. Supported decompressors: {SupportedNames}.",
                nameof(name))
        };
    }

    public static string SupportedNames =>
        $"{SharpCompressArchiveBZip2Decompressor.DecompressorName}, {SharpZipLibArchiveBZip2Decompressor.DecompressorName}";

    private static string NormalizeName(string name) =>
        name.Trim().ToLowerInvariant();
}
