namespace RadarPulse.Infrastructure.Archive;

public static class ArchiveBZip2Decompressors
{
    public const string DefaultName = ReusableArchiveBZip2Decompressor.DecompressorName;

    public static IArchiveBZip2Decompressor Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return NormalizeName(name) switch
        {
            ReusableArchiveBZip2Decompressor.DecompressorName => new ReusableArchiveBZip2Decompressor(),
            SharpCompressArchiveBZip2Decompressor.DecompressorName => new SharpCompressArchiveBZip2Decompressor(),
            SharpZipLibArchiveBZip2Decompressor.DecompressorName => new SharpZipLibArchiveBZip2Decompressor(),
            _ => throw new ArgumentException(
                $"Unknown decompressor '{name}'. Supported decompressors: {SupportedNames}.",
                nameof(name))
        };
    }

    public static string SupportedNames =>
        $"{ReusableArchiveBZip2Decompressor.DecompressorName}, {SharpZipLibArchiveBZip2Decompressor.DecompressorName}, {SharpCompressArchiveBZip2Decompressor.DecompressorName}";

    private static string NormalizeName(string name) =>
        name.Trim().ToLowerInvariant();
}
