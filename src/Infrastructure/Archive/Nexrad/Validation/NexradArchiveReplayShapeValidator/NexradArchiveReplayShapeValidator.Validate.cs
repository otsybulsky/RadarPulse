using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeValidator
{
    /// </summary>
    public ArchiveTwoReplayShapeValidationResult ValidateFile(
        string filePath,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");
        }

        ArchiveTwoReplayShapeValidationFileResult[] files = ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo)
            ? [ValidateArchiveTwoFile(fileInfo, degreeOfParallelism, cancellationToken)]
            : [];

        return new ArchiveTwoReplayShapeValidationResult(
            decompressor.Name,
            degreeOfParallelism,
            ExaminedFileCount: 1,
            SkippedFileCount: files.Length == 0 ? 1 : 0,
            files);
    }

    /// <summary>
    /// Validates replay-shape consistency for matching Archive II files in a cache directory.
    /// </summary>
    public ArchiveTwoReplayShapeValidationResult ValidateCache(
        string cachePath,
        string? radarId,
        int maxFiles,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Max files must be greater than zero.");
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");
        }

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var files = new List<ArchiveTwoReplayShapeValidationFileResult>();
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

            files.Add(ValidateArchiveTwoFile(fileInfo, degreeOfParallelism, cancellationToken));
        }

        return new ArchiveTwoReplayShapeValidationResult(
            decompressor.Name,
            degreeOfParallelism,
            examinedFiles,
            skippedFiles,
            files);
    }
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
}
