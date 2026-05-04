namespace RadarPulse.Domain.Archive;

public sealed record HistoricalArchiveRequest(
    DateOnly Date,
    IReadOnlyCollection<string>? RadarIds = null,
    bool AllRadars = false,
    int? MaxFiles = null,
    long? MaxBytes = null)
{
    public IReadOnlyCollection<string> NormalizedRadarIds =>
        RadarIds?.Select(NormalizeRadarId).Distinct().ToArray()
        ?? Array.Empty<string>();

    public static string NormalizeRadarId(string radarId)
    {
        var normalized = radarId.Trim().ToUpperInvariant();
        if (normalized.Length != 4 || !normalized.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException("Radar id must be a 4-character alphanumeric identifier.", nameof(radarId));
        }

        return normalized;
    }

    public void ValidateForDiscovery()
    {
        if (!AllRadars && NormalizedRadarIds.Count == 0)
        {
            throw new InvalidOperationException("Specify at least one radar id or pass --all-radars explicitly.");
        }

        if (AllRadars && NormalizedRadarIds.Count > 0)
        {
            throw new InvalidOperationException("--all-radars cannot be combined with --radar.");
        }

        if (MaxFiles is <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (MaxBytes is <= 0)
        {
            throw new InvalidOperationException("--max-bytes must be greater than zero.");
        }
    }
}
