namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Dense dictionary entry mapping an ordinal id to canonical text.
/// </summary>
public readonly record struct DenseIdentityCatalogEntry
{
    /// <summary>
    /// Creates a dense catalog entry.
    /// </summary>
    public DenseIdentityCatalogEntry(int id, string text)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        ArgumentNullException.ThrowIfNull(text);

        Id = id;
        Text = text;
    }

    /// <summary>
    /// Dense zero-based id assigned by the catalog.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Canonical identity text for the id.
    /// </summary>
    public string Text { get; }
}
