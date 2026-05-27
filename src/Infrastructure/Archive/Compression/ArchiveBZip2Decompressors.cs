namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Factory for supported Archive II BZip2 decompressor implementations.
/// </summary>
public static class ArchiveBZip2Decompressors
{
    /// <summary>
    /// Default decompressor name used by archive commands and services.
    /// </summary>
    public const string DefaultName = ReusableArchiveBZip2Decompressor.DecompressorName;

    /// <summary>
    /// Creates a decompressor implementation by its normalized name.
    /// </summary>
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

    /// <summary>
    /// Gets a comma-separated list of supported decompressor names.
    /// </summary>
    public static string SupportedNames =>
        $"{ReusableArchiveBZip2Decompressor.DecompressorName}, {SharpZipLibArchiveBZip2Decompressor.DecompressorName}, {SharpCompressArchiveBZip2Decompressor.DecompressorName}";

    private static string NormalizeName(string name) =>
        name.Trim().ToLowerInvariant();
}
