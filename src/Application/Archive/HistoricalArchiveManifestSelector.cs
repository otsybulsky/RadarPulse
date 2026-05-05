using RadarPulse.Domain.Archive;

namespace RadarPulse.Application.Archive;

public sealed class HistoricalArchiveManifestSelector
{
    public HistoricalArchiveManifest Select(
        HistoricalArchiveManifest manifest,
        IReadOnlyCollection<string>? radarIds,
        int? maxFiles,
        long? maxBytes)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (maxFiles is <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (maxBytes is <= 0)
        {
            throw new InvalidOperationException("--max-bytes must be greater than zero.");
        }

        var normalizedRadarIds = radarIds?
            .Select(HistoricalArchiveRequest.NormalizeRadarId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedFiles = new List<HistoricalArchiveFile>();
        long selectedBytes = 0;

        foreach (var file in manifest.Files)
        {
            if (normalizedRadarIds is { Count: > 0 } &&
                !normalizedRadarIds.Contains(file.RadarId))
            {
                continue;
            }

            if (maxFiles is { } fileLimit && selectedFiles.Count >= fileLimit)
            {
                break;
            }

            if (maxBytes is { } byteLimit && selectedBytes + file.SizeBytes > byteLimit)
            {
                break;
            }

            selectedFiles.Add(file);
            selectedBytes += file.SizeBytes;
        }

        return new HistoricalArchiveManifest(manifest.ArchiveDate, selectedFiles);
    }
}
