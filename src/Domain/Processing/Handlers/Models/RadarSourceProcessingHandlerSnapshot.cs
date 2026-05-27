namespace RadarPulse.Domain.Processing;

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

    public int SourceId { get; }

    public IReadOnlyList<RadarSourceProcessingSnapshotValue> Values => values;

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
