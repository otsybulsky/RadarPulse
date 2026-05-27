namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Ordered dictionary entries added between two dictionary versions.
/// </summary>
/// <remarks>
/// Delta entries are dense and start at the id represented by
/// <see cref="FromVersion"/>. Applying a delta requires the target snapshot to
/// have the same catalog name and matching start version.
/// </remarks>
public sealed class DenseIdentityCatalogDelta
{
    private readonly DenseIdentityCatalogEntry[] entries;

    internal DenseIdentityCatalogDelta(
        string name,
        DictionaryVersion fromVersion,
        DictionaryVersion toVersion,
        DenseIdentityCatalogEntry[] entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(entries);

        if (toVersion.Value < fromVersion.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(toVersion));
        }

        var expectedCount = checked((int)(toVersion.Value - fromVersion.Value));
        if (entries.Length != expectedCount)
        {
            throw new ArgumentException("Delta entries must match the dictionary version range.", nameof(entries));
        }

        var expectedId = checked((int)(fromVersion.Value - DictionaryVersion.Initial.Value));
        var seenText = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Id != expectedId + i)
            {
                throw new ArgumentException("Delta entries must be dense and ordered by id.", nameof(entries));
            }

            if (!seenText.Add(entry.Text))
            {
                throw new ArgumentException("Delta entries must not contain duplicate text.", nameof(entries));
            }
        }

        Name = name;
        FromVersion = fromVersion;
        ToVersion = toVersion;
        this.entries = entries;
    }

    /// <summary>
    /// Catalog name the delta belongs to.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Version the delta starts from.
    /// </summary>
    public DictionaryVersion FromVersion { get; }

    /// <summary>
    /// Version reached after applying the delta.
    /// </summary>
    public DictionaryVersion ToVersion { get; }

    /// <summary>
    /// Number of entries included in the delta.
    /// </summary>
    public int Count => entries.Length;

    /// <summary>
    /// Dense entries ordered by id.
    /// </summary>
    public ReadOnlyMemory<DenseIdentityCatalogEntry> Entries => entries;

    internal DenseIdentityCatalogEntry[] EntryArray => entries;
}
