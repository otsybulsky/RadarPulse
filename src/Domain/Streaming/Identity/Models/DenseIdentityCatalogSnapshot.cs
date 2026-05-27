namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Immutable snapshot of a dense identity catalog at a dictionary version.
/// </summary>
public sealed class DenseIdentityCatalogSnapshot
{
    private readonly DenseIdentityCatalogEntry[] entries;
    private readonly Dictionary<string, int> textToId;

    internal DenseIdentityCatalogSnapshot(
        string name,
        DictionaryVersion version,
        DenseIdentityCatalogEntry[] entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(entries);

        var expectedVersionValue = checked(DictionaryVersion.Initial.Value + entries.Length);
        if (version.Value != expectedVersionValue)
        {
            throw new ArgumentException("Snapshot version must match the visible entry count.", nameof(version));
        }

        textToId = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Id != i)
            {
                throw new ArgumentException("Snapshot entries must be dense and ordered by id.", nameof(entries));
            }

            if (!textToId.TryAdd(entry.Text, entry.Id))
            {
                throw new ArgumentException("Snapshot entries must not contain duplicate text.", nameof(entries));
            }
        }

        Name = name;
        Version = version;
        this.entries = entries;
    }

    /// <summary>
    /// Catalog name represented by the snapshot.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Dictionary version represented by the visible entries.
    /// </summary>
    public DictionaryVersion Version { get; }

    /// <summary>
    /// Number of visible entries.
    /// </summary>
    public int Count => entries.Length;

    /// <summary>
    /// Dense entries ordered by id.
    /// </summary>
    public ReadOnlyMemory<DenseIdentityCatalogEntry> Entries => entries;

    /// <summary>
    /// Looks up an id by canonical text.
    /// </summary>
    public bool TryGetId(string text, out int id)
    {
        ArgumentNullException.ThrowIfNull(text);
        return textToId.TryGetValue(text, out id);
    }

    /// <summary>
    /// Looks up an id by canonical UTF-16 text.
    /// </summary>
    public bool TryGetId(ReadOnlySpan<char> text, out int id)
    {
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].Text.AsSpan().SequenceEqual(text))
            {
                id = entries[i].Id;
                return true;
            }
        }

        id = default;
        return false;
    }

    /// <summary>
    /// Looks up an id by canonical ASCII/UTF-8 bytes.
    /// </summary>
    public bool TryGetId(ReadOnlySpan<byte> utf8Text, out int id)
    {
        for (var i = 0; i < entries.Length; i++)
        {
            if (AsciiTextEquals(entries[i].Text, utf8Text))
            {
                id = entries[i].Id;
                return true;
            }
        }

        id = default;
        return false;
    }

    /// <summary>
    /// Looks up canonical text by dense id.
    /// </summary>
    public bool TryGetText(int id, out string? text)
    {
        if ((uint)id < (uint)entries.Length)
        {
            text = entries[id].Text;
            return true;
        }

        text = null;
        return false;
    }

    /// <summary>
    /// Applies a compatible dense catalog delta to produce a newer snapshot.
    /// </summary>
    public DenseIdentityCatalogSnapshot Apply(DenseIdentityCatalogDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (!StringComparer.Ordinal.Equals(Name, delta.Name))
        {
            throw new ArgumentException("Delta belongs to a different catalog.", nameof(delta));
        }

        if (delta.FromVersion != Version)
        {
            throw new ArgumentException("Delta does not start from this snapshot version.", nameof(delta));
        }

        if (delta.Count == 0)
        {
            return this;
        }

        var combined = new DenseIdentityCatalogEntry[entries.Length + delta.Count];
        Array.Copy(entries, combined, entries.Length);
        Array.Copy(delta.EntryArray, 0, combined, entries.Length, delta.Count);
        return new DenseIdentityCatalogSnapshot(Name, delta.ToVersion, combined);
    }

    private static bool AsciiTextEquals(string text, ReadOnlySpan<byte> utf8Text)
    {
        if (text.Length != utf8Text.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != utf8Text[i])
            {
                return false;
            }
        }

        return true;
    }
}
