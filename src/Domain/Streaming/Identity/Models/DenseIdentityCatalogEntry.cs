namespace RadarPulse.Domain.Streaming;

public readonly record struct DenseIdentityCatalogEntry
{
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

    public int Id { get; }

    public string Text { get; }
}
