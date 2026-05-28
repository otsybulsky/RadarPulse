using System.Threading;

namespace RadarPulse.Domain.Streaming;

public sealed partial class DenseIdentityCatalog
{
    /// <summary>
    /// Attempts to look up an existing id by canonical string.
    /// </summary>
    public bool TryGetId(string text, out int id)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!canonicalizationPolicy.IsCanonical(text.AsSpan()))
        {
            id = default;
            return false;
        }

        return textToId.TryGetValue(text, out id);
    }

    /// <summary>
    /// Attempts to look up an existing id by canonical UTF-16 text.
    /// </summary>
    public bool TryGetId(ReadOnlySpan<char> text, out int id)
    {
        if (!canonicalizationPolicy.IsCanonical(text))
        {
            id = default;
            return false;
        }

        return textSpanLookup.TryGetValue(text, out id);
    }

    /// <summary>
    /// Attempts to look up an existing id by canonical UTF-8 bytes.
    /// </summary>
    public bool TryGetId(ReadOnlySpan<byte> utf8Text, out int id)
    {
        if (!canonicalizationPolicy.IsCanonical(utf8Text))
        {
            id = default;
            return false;
        }

        var hash = GetUtf8Hash(utf8Text);
        var bucket = Volatile.Read(ref utf8Buckets[hash & (uint)(utf8Buckets.Length - 1)]);
        if (bucket is not null && bucket.TryGetId(utf8Text, hash, out id))
        {
            return true;
        }

        id = default;
        return false;
    }

    /// <summary>
    /// Attempts to look up canonical text by dense id.
    /// </summary>
    public bool TryGetText(int id, out string? text)
    {
        var reverse = Volatile.Read(ref idToText);
        if ((uint)id < (uint)reverse.Length)
        {
            text = Volatile.Read(ref reverse[id]);
            return text is not null;
        }

        text = null;
        return false;
    }
}
