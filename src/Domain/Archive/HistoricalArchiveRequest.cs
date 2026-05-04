namespace RadarPulse.Domain.Archive;

public sealed record HistoricalArchiveRequest(
    DateOnly Date,
    IReadOnlyCollection<string>? RadarIds = null,
    bool AllRadars = false,
    int? MaxFiles = null,
    long? MaxBytes = null)
{
    public IReadOnlyCollection<string> NormalizedRadarIds =>
        RadarIds?.Select(r => r.Trim().ToUpperInvariant()).Where(r => r.Length > 0).Distinct().ToArray()
        ?? Array.Empty<string>();

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
