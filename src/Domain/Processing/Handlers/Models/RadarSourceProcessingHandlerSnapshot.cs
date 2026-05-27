namespace RadarPulse.Domain.Processing;

/// <summary>
/// Exported handler snapshot values for one source.
/// </summary>
public sealed class RadarSourceProcessingHandlerSnapshot
{
    private readonly IReadOnlyList<RadarSourceProcessingSnapshotValue> values;

    internal RadarSourceProcessingHandlerSnapshot(
        int sourceId,
        RadarSourceProcessingSnapshotValue[] values)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentNullException.ThrowIfNull(values);

        SourceId = sourceId;
        this.values = Array.AsReadOnly((RadarSourceProcessingSnapshotValue[])values.Clone());
    }

    /// <summary>
    /// Source id associated with the handler values.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Exported handler values in descriptor field order.
    /// </summary>
    public IReadOnlyList<RadarSourceProcessingSnapshotValue> Values => values;

    /// <summary>
    /// Attempts to find a handler snapshot value by exported field name.
    /// </summary>
    public bool TryGetValue(
        string name,
        out RadarSourceProcessingSnapshotValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var candidate in values)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}
