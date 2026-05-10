using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class NexradArchiveDecompressionValidatorCorpusTests
{
    [CorpusFact]
    public void ValidateCachedArchiveCorpusAgainstSharpZipLib()
    {
        var cachePath = Environment.GetEnvironmentVariable("RADARPULSE_NEXRAD_CORPUS");
        if (string.IsNullOrWhiteSpace(cachePath))
        {
            cachePath = Path.Combine("data", "nexrad");
        }

        cachePath = ResolveCorpusPath(cachePath);
        var maxFiles = int.TryParse(
            Environment.GetEnvironmentVariable("RADARPULSE_NEXRAD_CORPUS_MAX_FILES"),
            out var parsedMaxFiles)
            ? parsedMaxFiles
            : 20;
        var radarId = Environment.GetEnvironmentVariable("RADARPULSE_NEXRAD_CORPUS_RADAR");

        var result = new NexradArchiveDecompressionValidator().ValidateCache(
            cachePath,
            radarId,
            maxFiles,
            CancellationToken.None);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Files.Select(file => file.Diagnostic)));
        Assert.True(result.ComparedFileCount > 0);
    }

    private static string ResolveCorpusPath(string corpusPath)
    {
        if (Path.IsPathRooted(corpusPath))
        {
            return corpusPath;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, corpusPath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(corpusPath);
    }
}
