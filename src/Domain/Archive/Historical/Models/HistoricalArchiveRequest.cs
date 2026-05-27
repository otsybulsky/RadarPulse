namespace RadarPulse.Domain.Archive;

/// <summary>
/// Request for discovering historical archive files for a date and radar selection.
/// </summary>
/// <remarks>
/// A request can target explicit radar ids or all radars, with optional file and byte limits used by discovery and
/// later manifest selection.
/// </remarks>
public sealed record HistoricalArchiveRequest(
    DateOnly Date,
    IReadOnlyCollection<string>? RadarIds = null,
    bool AllRadars = false,
    int? MaxFiles = null,
    long? MaxBytes = null)
{
    /// <summary>
    /// Gets normalized distinct four-character radar ids, or an empty collection when all-radar mode is used.
    /// </summary>
    public IReadOnlyCollection<string> NormalizedRadarIds =>
        RadarIds?.Select(NormalizeRadarId).Distinct().ToArray()
        ?? Array.Empty<string>();

    /// <summary>
    /// Normalizes a radar id to the accepted four-character uppercase archive key format.
    /// </summary>
    public static string NormalizeRadarId(string radarId)
    {
        var normalized = radarId.Trim().ToUpperInvariant();
        if (normalized.Length != 4 || !normalized.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException("Radar id must be a 4-character alphanumeric identifier.", nameof(radarId));
        }

        return normalized;
    }

    /// <summary>
    /// Validates that the request has a usable discovery scope and positive optional limits.
    /// </summary>
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
